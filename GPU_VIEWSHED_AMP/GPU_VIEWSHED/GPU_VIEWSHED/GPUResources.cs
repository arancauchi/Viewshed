using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SlimDX;
using System.Diagnostics;



namespace Why.DataFlowOperators.GPUResources
{

    internal interface IBufferAccessor<T>
        where T : struct
    {

        #region Public access methods.

        List<T> ReadData();
        List<T> ReadData(int itemCount);
        List<ReadT> ReadData<ReadT>() where ReadT : struct;
        List<ReadT> ReadData<ReadT>(int itemCount) where ReadT : struct;

        void WriteData(List<T> data);

        DataReadRequest<T> QueueReadData();
        DataReadRequest<T> QueueReadData(int itemCount);
        DataReadRequest<T, ReadT> QueueReadData<ReadT>() where ReadT : struct;
        DataReadRequest<T, ReadT> QueueReadData<ReadT>(int itemCount) where ReadT : struct;

        void CancelQueuedReads();

        #endregion

    }



    internal class BufferManager<T> : IBufferAccessor<T>
        where T : struct
    {

        #region Public properties.

        public SlimDX.Direct3D11.Device Device { get; private set; }
        public SlimDX.Direct3D11.Resource Resource { get; private set; }
        public SlimDX.Direct3D11.Resource StagingResource { get; private set; }

        public int TypeSize { get; private set; }
        public int ElementCount { get; private set; }
        public int StagingElementCount { get; private set; }

        #endregion

        #region Constructors.

        public BufferManager(SlimDX.Direct3D11.Device device, SlimDX.Direct3D11.Resource resource, SlimDX.Direct3D11.Resource stagingResource, int elementCount, int stagingElementCount)
        {
            Device = device;
            Resource = resource;
            StagingResource = stagingResource;

            TypeSize = Marshal.SizeOf(typeof(T));
            ElementCount = elementCount;

            m_stagingBufferReadRequestReference = null;
            StagingElementCount = (stagingElementCount == 0) ? ElementCount : stagingElementCount;
        }

        #endregion

        #region Public access methods.

        public List<T> ReadData()
        {
            return ReadData(0);
        }

        public List<T> ReadData(int itemCount)
        {
            return ReadData<T>(itemCount);
        }

        public List<ReadT> ReadData<ReadT>()
            where ReadT : struct
        {
            return ReadData<ReadT>(0);
        }

        public List<ReadT> ReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            EmptyStagingBuffer();
            CopyBufferToStagingBuffer(itemCount);
            return ReadStagingBuffer<ReadT>(itemCount);
        }

        public void WriteData(List<T> data)
        {
            using (DataStream dataStream = new DataStream(data.Count * TypeSize, false, true)) {
                data.SerializeTo<T>(dataStream);
                dataStream.Seek(0L, System.IO.SeekOrigin.Begin);
                DataBox dataBox = new DataBox(TypeSize * data.Count, TypeSize * data.Count, dataStream);
                Device.ImmediateContext.UpdateSubresource(dataBox, Resource, 0);
            }
        }

        public DataReadRequest<T> QueueReadData()
        {
            return QueueReadData(0);
        }

        public DataReadRequest<T> QueueReadData(int itemCount)
        {
            EmptyStagingBuffer();

            CopyBufferToStagingBuffer(itemCount);

            DataReadRequest<T> stagingBufferReadRequest = new DataReadRequest<T>(this, itemCount);
            m_stagingBufferReadRequestReference = new WeakReference(stagingBufferReadRequest);
            return stagingBufferReadRequest;
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>()
            where ReadT : struct
        {
            return QueueReadData<ReadT>(0);
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            EmptyStagingBuffer();

            CopyBufferToStagingBuffer(itemCount);

            DataReadRequest<T, ReadT> stagingBufferReadRequest = new DataReadRequest<T, ReadT>(this, itemCount);
            m_stagingBufferReadRequestReference = new WeakReference(stagingBufferReadRequest);
            return stagingBufferReadRequest;
        }

        public void CancelQueuedReads()
        {
            m_stagingBufferReadRequestReference = null;
        }

        public void PrepareForTransfer()
        {
            EmptyStagingBuffer();
        }

        #endregion

        #region Internal access methods.

        internal List<ReadT> GetQueuedData<ReadT>(DataReadRequest<T, ReadT> dataReadRequest, int itemCount)
            where ReadT : struct
        {
            if (m_stagingBufferReadRequestReference == null) {
                return null;
            }
            if (dataReadRequest != m_stagingBufferReadRequestReference.Target) {
                return null;
            }

            List<ReadT> data = ReadStagingBuffer<ReadT>(itemCount);

            m_stagingBufferReadRequestReference = null;

            return data;
        }

        #endregion

        #region Private methods.

        private void CopyBufferToStagingBuffer(int itemCount)
        {
            if (itemCount == 0) {
                itemCount = StagingElementCount;
            }

            if (itemCount == ElementCount) {
                Device.ImmediateContext.CopyResource(Resource, StagingResource);
            } else {
                SlimDX.Direct3D11.ResourceRegion region = new SlimDX.Direct3D11.ResourceRegion(0, 0, 0, TypeSize * itemCount, 1, 1);
                Device.ImmediateContext.CopySubresourceRegion(Resource, 0, region, StagingResource, 0, 0, 0, 0);
            }
        }

        private List<ReadT> ReadStagingBuffer<ReadT>(int itemCount)
            where ReadT : struct
        {
            if (itemCount == 0) {
                itemCount = StagingElementCount;
            }

            DataBox dataBox = Device.ImmediateContext.MapSubresource(StagingResource, 0, TypeSize * itemCount, SlimDX.Direct3D11.MapMode.Read, SlimDX.Direct3D11.MapFlags.None);
            List<ReadT> result = new List<ReadT>(itemCount);
            result.Resize(itemCount);
            result.DeserializeFrom<ReadT>(dataBox.Data);
            Device.ImmediateContext.UnmapSubresource(StagingResource, 0);

            return result;
        }

        private void EmptyStagingBuffer()
        {
            if (m_stagingBufferReadRequestReference != null) {
                DataReadRequest stagingBufferReadRequest = m_stagingBufferReadRequestReference.Target as DataReadRequest;
                if (stagingBufferReadRequest != null) {
                    stagingBufferReadRequest.ObtainData();
                    stagingBufferReadRequest = null;
                }
            }
        }

        #endregion

        #region Private fields.

        private WeakReference m_stagingBufferReadRequestReference;

        #endregion

    }



    public abstract class DataReadRequest
    {

        #region Internal access methods.

        internal abstract void ObtainData();

        #endregion

    }



    public class DataReadRequest<BufferT, ReadT> : DataReadRequest
        where BufferT : struct
        where ReadT : struct
    {

        #region Public properties.

        public List<ReadT> Data
        {
            get
            {
                if (m_data == null) {
                    ObtainData();
                }

                return m_data;
            }
        }

        #endregion

        #region Constructors.

        internal DataReadRequest(BufferManager<BufferT> bufferManager)
            : this(bufferManager, 0)
        {
        }

        internal DataReadRequest(BufferManager<BufferT> bufferManager, int itemCount)
        {
            m_bufferManager = bufferManager;
            m_itemCount = itemCount;
            m_data = null;
        }

        #endregion

        #region Internal access methods.

        internal override void ObtainData()
        {
            m_data = m_bufferManager.GetQueuedData<ReadT>(this, m_itemCount);
            if (m_data == null) {
                throw new Exception("Attempt to read cancelled data from a buffer.");
            }
        }

        #endregion

        #region Private fields.

        private BufferManager<BufferT> m_bufferManager;
        private int m_itemCount;
        private List<ReadT> m_data;

        #endregion

    }



    public class DataReadRequest<T> : DataReadRequest<T, T>
        where T : struct
    {

        #region Constructors.

        internal DataReadRequest(BufferManager<T> bufferManager)
            : base(bufferManager)
        {
        }

        internal DataReadRequest(BufferManager<T> bufferManager, int itemCount)
            : base(bufferManager, itemCount)
        {
        }

        #endregion

    }



    public static class ResourceDisposer
    {

        #region Public static access methods.

        public static T DisposeResource<T>(T resource)
            where T : IDisposable
        {
            if (resource != null) {
                resource.Dispose();
            }

            return default(T);
        }

        public static void ReleaseResource(IResourceWrapper resourceWrapper)
        {
            if (resourceWrapper != null) {
                resourceWrapper.ReleaseResource();
            }
        }

        #endregion

    }



    public interface IResourceWrapper : IDisposable
    {
        void ReleaseResource();
    }



    public abstract class ShaderResource : IResourceWrapper, IDisposable
    {

        #region Public properties.

        public SlimDX.Direct3D11.Device Device { get; private set; }
        public string Name { get; private set; }

        public abstract SlimDX.Direct3D11.Resource Resource { get; }
        public abstract int GetTypeSize();

        public bool Disposed { get; private set; }

        #endregion

        #region Constructors.

        protected ShaderResource(SlimDX.Direct3D11.Device device, string name)
        {
            Device = device;
            Name = name;
        }

        #endregion

        #region Public access methods.

        public abstract void ReleaseResource();

        public virtual void Dispose()
        {
            Debug.Assert(!Disposed);

            ReleaseResource();

            Device = null;
            Name = null;

            Disposed = true;
        }

        #endregion

    }

    public interface IConstantBuffer
    {
        SlimDX.Direct3D11.Buffer Buffer { get; }

        int DefaultSlot { get; }
    }

    public interface IShaderResourceView
    {
        SlimDX.Direct3D11.ShaderResourceView ShaderResourceView { get; }

        int DefaultSlot { get; }
    }

    public interface IUnorderedAccessView
    {
        SlimDX.Direct3D11.UnorderedAccessView UnorderedAccessView { get; }

        int DefaultSlot { get; }
    }



    public abstract class ConstantBuffer : ShaderResource, IConstantBuffer
    {

        #region Public properties.

        public SlimDX.Direct3D11.Buffer Buffer { get; protected set; }
        public override SlimDX.Direct3D11.Resource Resource { get { return Buffer; } }

        public int DefaultSlot { get; private set; }

        #endregion

        #region Constructors.

        protected ConstantBuffer(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name)
        {
            DefaultSlot = defaultSlot;
        }

        #endregion

        #region Public access methods.

        public override void ReleaseResource()
        {
            Debug.Assert(!Disposed);

            Buffer = ResourceDisposer.DisposeResource(Buffer);
        }

        #endregion

    }

    public sealed class ConstantBuffer<T> : ConstantBuffer
        where T : struct
    {

        #region Public properties.

        public int TypeSize { get; private set; }
        public override int GetTypeSize() { return TypeSize; }
        public T Value { get; private set; }

        #endregion

        #region Constructors.

        public ConstantBuffer(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name, defaultSlot)
        {
            TypeSize = Marshal.SizeOf(typeof(T));
        }

        #endregion

        #region Public access methods.

        public void CreateResource()
        {
            Debug.Assert(!Disposed);

            SlimDX.Direct3D11.BufferDescription bufferDescription = new SlimDX.Direct3D11.BufferDescription(TypeSize, SlimDX.Direct3D11.ResourceUsage.Default, SlimDX.Direct3D11.BindFlags.ConstantBuffer, SlimDX.Direct3D11.CpuAccessFlags.None, SlimDX.Direct3D11.ResourceOptionFlags.None, TypeSize);

            Buffer = new SlimDX.Direct3D11.Buffer(Device, null, bufferDescription);

            Buffer.DebugName = Name;
        }

        public void WriteData(T data)
        {
            Debug.Assert(!Disposed);

            Value = data;

            using (DataStream dataStream = new DataStream(new T[] { data }, true, true)) {
                DataBox dataBox = new DataBox(TypeSize, TypeSize, dataStream);
                Device.ImmediateContext.UpdateSubresource(dataBox, Buffer, 0);
            }
        }

        #endregion

    }



    public abstract class StructuredBuffer : ShaderResource, IShaderResourceView, IUnorderedAccessView
    {

        #region Public properties.

        public SlimDX.Direct3D11.Buffer Buffer { get; protected set; }
        public override SlimDX.Direct3D11.Resource Resource { get { return Buffer; } }
        public SlimDX.Direct3D11.Buffer StagingBuffer { get; protected set; }
        public SlimDX.Direct3D11.ShaderResourceView ShaderResourceView { get; protected set; }
        public SlimDX.Direct3D11.UnorderedAccessView UnorderedAccessView { get; protected set; }

        public int DefaultSlot { get; private set; }

        #endregion

        #region Constructors.

        protected StructuredBuffer(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name)
        {
            DefaultSlot = defaultSlot;
        }

        #endregion

        #region Public access methods.

        public override void ReleaseResource()
        {
            Debug.Assert(!Disposed);

            Buffer = ResourceDisposer.DisposeResource(Buffer);
            StagingBuffer = ResourceDisposer.DisposeResource(StagingBuffer);
            ShaderResourceView = ResourceDisposer.DisposeResource(ShaderResourceView);
            UnorderedAccessView = ResourceDisposer.DisposeResource(UnorderedAccessView);
        }

        #endregion

    }

    public class StructuredBuffer<T> : StructuredBuffer, IBufferAccessor<T>
        where T : struct
    {

        #region Public properties.

        public int TypeSize { get; private set; }
        public override int GetTypeSize() { return TypeSize; }
        public int ElementCount { get; private set; }
        public int StagingElementCount { get; private set; }

        #endregion

        #region Constructors.

        public StructuredBuffer(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name, defaultSlot)
        {
            TypeSize = Marshal.SizeOf(typeof(T));
        }

        #endregion

        #region Public access methods.

        public void CreateResource(int elementCount, bool createStagingBuffer)
        {
            Debug.Assert(!Disposed);

            ElementCount = elementCount;
            StagingElementCount = 0;

            CreateBuffers(createStagingBuffer);
        }

        public void CreateResource(int elementCount, int stagingElementCount)
        {
            Debug.Assert(!Disposed);

            ElementCount = elementCount;
            StagingElementCount = stagingElementCount;

            CreateBuffers(true);
        }

        public delegate void BufferResizeDelegate(SlimDX.Direct3D11.Buffer oldBuffer, SlimDX.Direct3D11.Buffer newBuffer, SlimDX.Direct3D11.ShaderResourceView transferOldSRV, SlimDX.Direct3D11.UnorderedAccessView transferNewUAV, int oldElementCount, int newElementCount);

        public void Resize(int newElementCount)
        {
            Debug.Assert(!Disposed);

            Resize(newElementCount, null, true);
        }

        public void Resize(int newElementCount, BufferResizeDelegate bufferResizeDelegate)
        {
            Debug.Assert(!Disposed);

            Resize(newElementCount, bufferResizeDelegate, false);
        }

        #endregion

        #region Public static access methods.

        public static void SwapBufferContents(StructuredBuffer<T> structuredBufferA, StructuredBuffer<T> structuredBufferB)
        {
            Debug.Assert(structuredBufferA.ElementCount == structuredBufferB.ElementCount);
            Debug.Assert(structuredBufferA.StagingElementCount == structuredBufferB.StagingElementCount);

            BufferManager<T> tempBufferManager = structuredBufferA.m_bufferManager;
            structuredBufferA.m_bufferManager = structuredBufferB.m_bufferManager;
            structuredBufferB.m_bufferManager = tempBufferManager;

            SlimDX.Direct3D11.Buffer tempBuffer = structuredBufferA.Buffer;
            structuredBufferA.Buffer = structuredBufferB.Buffer;
            structuredBufferB.Buffer = tempBuffer;

            SlimDX.Direct3D11.Buffer tempStagingBuffer = structuredBufferA.StagingBuffer;
            structuredBufferA.StagingBuffer = structuredBufferB.StagingBuffer;
            structuredBufferB.StagingBuffer = tempStagingBuffer;

            SlimDX.Direct3D11.ShaderResourceView tempShaderResourceView = structuredBufferA.ShaderResourceView;
            structuredBufferA.ShaderResourceView = structuredBufferB.ShaderResourceView;
            structuredBufferB.ShaderResourceView = tempShaderResourceView;

            SlimDX.Direct3D11.UnorderedAccessView tempUnorderedAccessView = structuredBufferA.UnorderedAccessView;
            structuredBufferA.UnorderedAccessView = structuredBufferB.UnorderedAccessView;
            structuredBufferB.UnorderedAccessView = tempUnorderedAccessView;
        }

        #endregion

        #region IBufferAccessor methods.

        public List<T> ReadData()
        {
            return m_bufferManager.ReadData();
        }

        public List<T> ReadData(int itemCount)
        {
            return m_bufferManager.ReadData(itemCount);
        }

        public List<ReadT> ReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>();
        }

        public List<ReadT> ReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>(itemCount);
        }

        public void WriteData(List<T> data)
        {
            m_bufferManager.WriteData(data);
        }

        public DataReadRequest<T> QueueReadData()
        {
            return m_bufferManager.QueueReadData();
        }

        public DataReadRequest<T> QueueReadData(int itemCount)
        {
            return m_bufferManager.QueueReadData(itemCount);
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>();
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>(itemCount);
        }

        public void CancelQueuedReads()
        {
            if (m_bufferManager != null) {
                m_bufferManager.CancelQueuedReads();
            }
        }

        #endregion

        #region Private methods.

        private void CreateBuffers(bool createStagingBuffer)
        {
            if (ElementCount <= 0 || ElementCount * TypeSize <= 0) {
                throw new Exception(string.Format("GPU buffer size overflow for \"{0}\" buffer.", Name));
            }

            SlimDX.Direct3D11.BufferDescription bufferDescription = new SlimDX.Direct3D11.BufferDescription();
            bufferDescription.SizeInBytes = ElementCount * TypeSize;
            bufferDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Default;
            bufferDescription.BindFlags = SlimDX.Direct3D11.BindFlags.ShaderResource | SlimDX.Direct3D11.BindFlags.UnorderedAccess;
            bufferDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.None;
            bufferDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.StructuredBuffer;
            bufferDescription.StructureByteStride = TypeSize;

            Buffer = new SlimDX.Direct3D11.Buffer(Device, null, bufferDescription);
            Buffer.DebugName = Name;


            SlimDX.Direct3D11.ShaderResourceViewDescription shaderResourceViewDescription = new SlimDX.Direct3D11.ShaderResourceViewDescription();
            shaderResourceViewDescription.Format = SlimDX.DXGI.Format.Unknown;
            shaderResourceViewDescription.Dimension = SlimDX.Direct3D11.ShaderResourceViewDimension.Buffer;
            shaderResourceViewDescription.ElementWidth = ElementCount;

            ShaderResourceView = new SlimDX.Direct3D11.ShaderResourceView(Device, Buffer, shaderResourceViewDescription);
            ShaderResourceView.DebugName = string.Format("{0} SRV", Name);


            SlimDX.Direct3D11.UnorderedAccessViewDescription unorderedAccessViewDescription = new SlimDX.Direct3D11.UnorderedAccessViewDescription();
            unorderedAccessViewDescription.Format = SlimDX.DXGI.Format.Unknown;
            unorderedAccessViewDescription.Dimension = SlimDX.Direct3D11.UnorderedAccessViewDimension.Buffer;
            unorderedAccessViewDescription.ElementCount = ElementCount;

            UnorderedAccessView = new SlimDX.Direct3D11.UnorderedAccessView(Device, Buffer, unorderedAccessViewDescription);
            UnorderedAccessView.DebugName = string.Format("{0} UAV", Name);


            if (createStagingBuffer) {
                SlimDX.Direct3D11.BufferDescription stagingBufferDescription = new SlimDX.Direct3D11.BufferDescription();
                int stagingElementCount = (StagingElementCount == 0) ? ElementCount : StagingElementCount;
                stagingBufferDescription.SizeInBytes = stagingElementCount * TypeSize;
                stagingBufferDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Staging;
                stagingBufferDescription.BindFlags = SlimDX.Direct3D11.BindFlags.None;
                stagingBufferDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.Read;
                stagingBufferDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.StructuredBuffer;
                stagingBufferDescription.StructureByteStride = TypeSize;

                StagingBuffer = new SlimDX.Direct3D11.Buffer(Device, null, stagingBufferDescription);
                StagingBuffer.DebugName = string.Format("{0} Staging", Name);
            }


            m_bufferManager = new BufferManager<T>(Device, Buffer, StagingBuffer, ElementCount, StagingElementCount);
        }

        private void Resize(int newElementCount, BufferResizeDelegate bufferResizeDelegate, bool ignoreIfSizeUnchanged)
        {
            if (ignoreIfSizeUnchanged && newElementCount == ElementCount) {
                return;
            }

            string name = Buffer.DebugName;
            bool createStagingBuffer = StagingBuffer != null;

            int oldElementCount = ElementCount;
            SlimDX.Direct3D11.Buffer oldBuffer = Buffer;
            SlimDX.Direct3D11.ShaderResourceView oldSRV = ShaderResourceView;
            BufferManager<T> oldBufferManager = m_bufferManager;

            oldBufferManager.PrepareForTransfer();

            if (bufferResizeDelegate != null) {
                //  Release the buffer reference before disposing so that
                //  we can keep the old buffer alive for the transfer.
                ShaderResourceView = null;
                Buffer = null;
            }

            ReleaseResource();

            ElementCount = newElementCount;
            CreateBuffers(createStagingBuffer);

            if (bufferResizeDelegate == null) {
                Device.ImmediateContext.ClearUnorderedAccessView(UnorderedAccessView, new int[] { 0, 0, 0, 0 });
            } else {
                bufferResizeDelegate(oldBuffer, Buffer, oldSRV, UnorderedAccessView, oldElementCount, newElementCount);

                //  Now dispose the old buffer manually.
                oldBuffer = ResourceDisposer.DisposeResource(oldBuffer);
                oldSRV = ResourceDisposer.DisposeResource(oldSRV);
            }
        }

        #endregion

        #region Private fields.

        private BufferManager<T> m_bufferManager;

        #endregion

    }



    public abstract class VertexBuffer : ShaderResource
    {

        #region Public properties.

        public SlimDX.Direct3D11.Buffer Buffer { get; protected set; }
        public override SlimDX.Direct3D11.Resource Resource { get { return Buffer; } }
        public SlimDX.Direct3D11.Buffer StagingBuffer { get; protected set; }

        public int DefaultSlot { get; private set; }

        #endregion

        #region Constructors.

        protected VertexBuffer(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name)
        {
            DefaultSlot = defaultSlot;
        }

        #endregion

        #region Public access methods.

        public override void ReleaseResource()
        {
            Debug.Assert(!Disposed);

            Buffer = ResourceDisposer.DisposeResource(Buffer);
            StagingBuffer = ResourceDisposer.DisposeResource(StagingBuffer);
        }

        #endregion

    }

    public sealed class VertexBuffer<T> : VertexBuffer, IBufferAccessor<T>
        where T : struct
    {

        #region Public properties.

        public int TypeSize { get; private set; }
        public override int GetTypeSize() { return TypeSize; }
        public int ElementCount { get; private set; }

        #endregion

        #region Constructors.

        public VertexBuffer(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name, defaultSlot)
        {
            TypeSize = Marshal.SizeOf(typeof(T));
        }

        #endregion

        #region Public access methods.

        public void CreateResource(int elementCount)
        {
            Debug.Assert(!Disposed);

            ElementCount = elementCount;

            SlimDX.Direct3D11.BufferDescription bufferDescription = new SlimDX.Direct3D11.BufferDescription();
            bufferDescription.SizeInBytes = ElementCount * TypeSize;
            bufferDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Default;
            bufferDescription.BindFlags = SlimDX.Direct3D11.BindFlags.VertexBuffer;
            bufferDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.None;
            bufferDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.None;
            bufferDescription.StructureByteStride = TypeSize;

            Buffer = new SlimDX.Direct3D11.Buffer(Device, null, bufferDescription);
            Buffer.DebugName = Name;


            SlimDX.Direct3D11.BufferDescription stagingBufferDescription = new SlimDX.Direct3D11.BufferDescription();
            stagingBufferDescription.SizeInBytes = ElementCount * TypeSize;
            stagingBufferDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Staging;
            stagingBufferDescription.BindFlags = SlimDX.Direct3D11.BindFlags.None;
            stagingBufferDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.Read;
            stagingBufferDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.None;
            stagingBufferDescription.StructureByteStride = TypeSize;

            StagingBuffer = new SlimDX.Direct3D11.Buffer(Device, null, stagingBufferDescription);
            StagingBuffer.DebugName = string.Format("{0} Staging", Name);


            m_bufferManager = new BufferManager<T>(Device, Buffer, StagingBuffer, ElementCount, ElementCount);
        }

        #endregion

        #region IBufferAccessor methods.

        public List<T> ReadData()
        {
            return m_bufferManager.ReadData();
        }

        public List<T> ReadData(int itemCount)
        {
            return m_bufferManager.ReadData();
        }

        public List<ReadT> ReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>();
        }

        public List<ReadT> ReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>();
        }

        public void WriteData(List<T> data)
        {
            m_bufferManager.WriteData(data);
        }

        public DataReadRequest<T> QueueReadData()
        {
            return m_bufferManager.QueueReadData();
        }

        public DataReadRequest<T> QueueReadData(int itemCount)
        {
            return m_bufferManager.QueueReadData(itemCount);
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>();
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>(itemCount);
        }

        public void CancelQueuedReads()
        {
            m_bufferManager.CancelQueuedReads();
        }

        #endregion

        #region Private fields.

        private BufferManager<T> m_bufferManager;

        #endregion

    }



    public abstract class IndexBuffer : ShaderResource
    {

        #region Public properties.

        public SlimDX.Direct3D11.Buffer Buffer { get; protected set; }
        public override SlimDX.Direct3D11.Resource Resource { get { return Buffer; } }
        public SlimDX.Direct3D11.Buffer StagingBuffer { get; protected set; }

        #endregion

        #region Constructors.

        protected IndexBuffer(SlimDX.Direct3D11.Device device, string name)
            : base(device, name)
        {
        }

        #endregion

        #region Public access methods.

        public override void ReleaseResource()
        {
            Debug.Assert(!Disposed);

            Buffer = ResourceDisposer.DisposeResource(Buffer);
            StagingBuffer = ResourceDisposer.DisposeResource(StagingBuffer);
        }

        #endregion

    }

    public sealed class IndexBuffer<T> : IndexBuffer, IBufferAccessor<T>
        where T : struct
    {

        #region Public properties.

        public int TypeSize { get; private set; }
        public override int GetTypeSize() { return TypeSize; }
        public int ElementCount { get; private set; }

        #endregion

        #region Constructors.

        public IndexBuffer(SlimDX.Direct3D11.Device device, string name)
            : base(device, name)
        {
            TypeSize = Marshal.SizeOf(typeof(T));
        }

        #endregion

        #region Public access methods.

        public void CreateResource(int elementCount)
        {
            Debug.Assert(!Disposed);

            ElementCount = elementCount;

            SlimDX.Direct3D11.BufferDescription bufferDescription = new SlimDX.Direct3D11.BufferDescription();
            bufferDescription.SizeInBytes = ElementCount * TypeSize;
            bufferDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Default;
            bufferDescription.BindFlags = SlimDX.Direct3D11.BindFlags.IndexBuffer;
            bufferDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.None;
            bufferDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.None;
            bufferDescription.StructureByteStride = TypeSize;

            Buffer = new SlimDX.Direct3D11.Buffer(Device, null, bufferDescription);
            Buffer.DebugName = Name;


            SlimDX.Direct3D11.BufferDescription stagingBufferDescription = new SlimDX.Direct3D11.BufferDescription();
            stagingBufferDescription.SizeInBytes = ElementCount * TypeSize;
            stagingBufferDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Staging;
            stagingBufferDescription.BindFlags = SlimDX.Direct3D11.BindFlags.None;
            stagingBufferDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.Read;
            stagingBufferDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.None;
            stagingBufferDescription.StructureByteStride = TypeSize;

            StagingBuffer = new SlimDX.Direct3D11.Buffer(Device, null, stagingBufferDescription);
            StagingBuffer.DebugName = string.Format("{0} Staging", Name);


            m_bufferManager = new BufferManager<T>(Device, Buffer, StagingBuffer, ElementCount, ElementCount);
        }

        #endregion

        #region IBufferAccessor methods.

        public List<T> ReadData()
        {
            return m_bufferManager.ReadData();
        }

        public List<T> ReadData(int itemCount)
        {
            return m_bufferManager.ReadData();
        }

        public List<ReadT> ReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>();
        }

        public List<ReadT> ReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>();
        }

        public void WriteData(List<T> data)
        {
            m_bufferManager.WriteData(data);
        }

        public DataReadRequest<T> QueueReadData()
        {
            return m_bufferManager.QueueReadData();
        }

        public DataReadRequest<T> QueueReadData(int itemCount)
        {
            return m_bufferManager.QueueReadData(itemCount);
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>();
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>(itemCount);
        }

        public void CancelQueuedReads()
        {
            m_bufferManager.CancelQueuedReads();
        }

        #endregion

        #region Private fields.

        private BufferManager<T> m_bufferManager;

        #endregion

    }



    public abstract class Texture2D : ShaderResource, IShaderResourceView
    {

        #region Public properties.

        public SlimDX.Direct3D11.Texture2D Texture { get; protected set; }
        public override SlimDX.Direct3D11.Resource Resource { get { return Texture; } }
        public SlimDX.Direct3D11.Texture2D StagingTexture { get; protected set; }
        public SlimDX.Direct3D11.ShaderResourceView ShaderResourceView { get; protected set; }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public SlimDX.DXGI.Format Format { get; private set; }

        public int DefaultSlot { get; private set; }

        #endregion

        #region Constructors.

        protected Texture2D(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name)
        {
            DefaultSlot = defaultSlot;
        }

        #endregion

        #region Public access methods.

        public override void ReleaseResource()
        {
            Debug.Assert(!Disposed);

            Texture = ResourceDisposer.DisposeResource(Texture);
            StagingTexture = ResourceDisposer.DisposeResource(StagingTexture);
            ShaderResourceView = ResourceDisposer.DisposeResource(ShaderResourceView);
        }

        #endregion

        #region Protected methods.

        protected void CreateResource(int width, int height, SlimDX.DXGI.Format format)
        {
            Debug.Assert(!Disposed);

            Width = width;
            Height = height;
            Format = format;
        }

        #endregion

    }

    public sealed class Texture2D<T> : Texture2D, IBufferAccessor<T>
        where T : struct
    {

        #region Public properties.

        public int TypeSize { get; private set; }
        public override int GetTypeSize() { return TypeSize; }

        #endregion

        #region Constructors.

        public Texture2D(SlimDX.Direct3D11.Device device, string name, int defaultSlot)
            : base(device, name, defaultSlot)
        {
            TypeSize = Marshal.SizeOf(typeof(T));
        }

        #endregion

        #region Public access methods.

        public void CreateResource(int width, int height, SlimDX.DXGI.Format format, List<T> initialData, bool createStagingBuffer)
        {
            Debug.Assert(!Disposed);

            base.CreateResource(width, height, format);


            SlimDX.Direct3D11.Texture2DDescription textureDescription = new SlimDX.Direct3D11.Texture2DDescription();
            textureDescription.Width = Width;
            textureDescription.Height = Height;
            textureDescription.MipLevels = 1;
            textureDescription.ArraySize = 1;
            textureDescription.Format = Format;
            textureDescription.SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0);
            textureDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Default;
            textureDescription.BindFlags = SlimDX.Direct3D11.BindFlags.ShaderResource | SlimDX.Direct3D11.BindFlags.RenderTarget;
            textureDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.None;
            textureDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.None;

            if (initialData != null) {
                using (DataStream data = new DataStream(initialData.Count * TypeSize, false, true)) {
                    initialData.SerializeTo<T>(data);
                    data.Seek(0L, System.IO.SeekOrigin.Begin);
                    DataRectangle dataRectangle = new DataRectangle(width * TypeSize, data);
                    Texture = new SlimDX.Direct3D11.Texture2D(Device, textureDescription, dataRectangle);
                }
            } else {
                Texture = new SlimDX.Direct3D11.Texture2D(Device, textureDescription);
            }

            Texture.DebugName = Name;


            SlimDX.Direct3D11.ShaderResourceViewDescription shaderResourceViewDescription = new SlimDX.Direct3D11.ShaderResourceViewDescription();
            shaderResourceViewDescription.Format = format;
            shaderResourceViewDescription.Dimension = SlimDX.Direct3D11.ShaderResourceViewDimension.Texture2D;
            shaderResourceViewDescription.MipLevels = 1;
            shaderResourceViewDescription.MostDetailedMip = 0;

            ShaderResourceView = new SlimDX.Direct3D11.ShaderResourceView(Device, Texture, shaderResourceViewDescription);
            ShaderResourceView.DebugName = string.Format("{0} SRV", Name);


            if (createStagingBuffer) {
                SlimDX.Direct3D11.Texture2DDescription stagingBufferDescription = new SlimDX.Direct3D11.Texture2DDescription();
                stagingBufferDescription.Width = Width;
                stagingBufferDescription.Height = Height;
                stagingBufferDescription.MipLevels = 1;
                stagingBufferDescription.ArraySize = 1;
                stagingBufferDescription.Format = Format;
                stagingBufferDescription.SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0);
                stagingBufferDescription.Usage = SlimDX.Direct3D11.ResourceUsage.Staging;
                stagingBufferDescription.BindFlags = SlimDX.Direct3D11.BindFlags.None;
                stagingBufferDescription.CpuAccessFlags = SlimDX.Direct3D11.CpuAccessFlags.Read;
                stagingBufferDescription.OptionFlags = SlimDX.Direct3D11.ResourceOptionFlags.None;

                StagingTexture = new SlimDX.Direct3D11.Texture2D(Device, stagingBufferDescription);
                StagingTexture.DebugName = string.Format("{0} Staging", Name);
            }


            m_bufferManager = new BufferManager<T>(Device, Texture, StagingTexture, Width * Height, Width * Height);
        }

        #endregion

        #region IBufferAccessor methods.

        public List<T> ReadData()
        {
            return m_bufferManager.ReadData();
        }

        public List<T> ReadData(int itemCount)
        {
            return m_bufferManager.ReadData();
        }

        public List<ReadT> ReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>();
        }

        public List<ReadT> ReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.ReadData<ReadT>();
        }

        public void WriteData(List<T> data)
        {
            m_bufferManager.WriteData(data);
        }

        public DataReadRequest<T> QueueReadData()
        {
            return m_bufferManager.QueueReadData();
        }

        public DataReadRequest<T> QueueReadData(int itemCount)
        {
            return m_bufferManager.QueueReadData(itemCount);
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>()
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>();
        }

        public DataReadRequest<T, ReadT> QueueReadData<ReadT>(int itemCount)
            where ReadT : struct
        {
            return m_bufferManager.QueueReadData<ReadT>(itemCount);
        }

        public void CancelQueuedReads()
        {
            m_bufferManager.CancelQueuedReads();
        }

        #endregion

        #region Private fields.

        private BufferManager<T> m_bufferManager;

        #endregion

    }



    public sealed class GPUShaderGroup : IDisposable
    {

        #region Public properties.

        public SlimDX.Direct3D11.Device Device { get; private set; }

        public IEnumerable<GPUShaderBase> Shaders { get { return m_shaderList; } }

        #endregion

        #region Constructors.

        public GPUShaderGroup(SlimDX.Direct3D11.Device device)
        {
            Device = device;

            m_shaderList = new List<GPUShaderBase>();
            m_renderShadersList = new List<GPURenderShaders>();
        }

        #endregion

        #region Internal access methods.

        internal void AddShader(GPUShaderBase shader)
        {
            m_shaderList.Add(shader);
        }

        internal void AddRenderShaders(GPURenderShaders renderShaders)
        {
            m_renderShadersList.Add(renderShaders);
        }

        #endregion

        #region Disposal methods.

        public void Dispose()
        {
            foreach (GPUShaderBase shader in m_shaderList) {
                shader.Dispose();
            }

            foreach (GPURenderShaders renderShaders in m_renderShadersList) {
                renderShaders.Dispose();
            }

            m_shaderList.Clear();
            m_renderShadersList.Clear();
        }

        #endregion

        #region Private fields.

        private List<GPUShaderBase> m_shaderList;
        private List<GPURenderShaders> m_renderShadersList;

        #endregion

    }



    public abstract class GPUShaderBase : IDisposable
    {

        #region Public properties.

        public GPUShaderGroup ShaderGroup { get; private set; }
        public SlimDX.Direct3D11.Device Device { get { return ShaderGroup.Device; } }

        public string Name { get; private set; }

        #endregion

        #region Constructors.

        public GPUShaderBase(GPUShaderGroup shaderGroup, string name)
        {
            ShaderGroup = shaderGroup;

            Name = name;

            if (shaderGroup != null) {
                shaderGroup.AddShader(this);
            }
        }

        #endregion

        #region Disposal methods.

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        ~GPUShaderBase()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            ShaderGroup = null;
        }

        #endregion

    }

    public abstract class GPUShader<ShaderType> : GPUShaderBase
        where ShaderType : SlimDX.Direct3D11.DeviceChild
    {

        #region Public properties.

        public ShaderType Shader { get; private set; }

        public SlimDX.D3DCompiler.ShaderSignature ShaderSignature { get; private set; }
        public SlimDX.D3DCompiler.ShaderBytecode ShaderBytecode { get; private set; }

        #endregion

        #region Constructors.

        public GPUShader(GPUShaderGroup shaderGroup, string filename, string entryPoint)
            : base(shaderGroup, entryPoint)
        {
            m_debugName = entryPoint;

            CompileShader(filename, entryPoint);
        }

        #endregion

        #region Private methods.

        private void CompileShader(string filename, string entryPoint)
        {
            CompileShader(filename, entryPoint, null);
        }

        private void CompileShader(string filename, string entryPoint, string profile)
        {
            if (string.IsNullOrEmpty(profile)) {
                if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.ComputeShader)) {
                    profile = "cs_5_0";
                } else if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.GeometryShader)) {
                    profile = "gs_5_0";
                } else if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.VertexShader)) {
                    profile = "vs_5_0";
                } else if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.PixelShader)) {
                    profile = "ps_5_0";
                }
            }

            CompileShaderWithRetry(filename, entryPoint, profile);
        }

        private void CompileShaderWithRetry(string filename, string entryPoint, string profile)
        {
            while (true) {
                bool loaded = false;

                //  Try loading source from debug resource directory
                //  first.
                if (!loaded) {
                    string resourceFileName = GetDebugResourceFileName(filename);

                    if (resourceFileName != null) {
                        loaded = true;

                        string objectFileName = Path.ChangeExtension(resourceFileName, ".fxo");
                        string arguments = string.Format("/nologo /T {0} /E {1} /Od /Zi /Gfp /Fo \"{2}\" \"{3}\"", profile, entryPoint, objectFileName, resourceFileName);

                        using (System.Diagnostics.Process generateFileProcess = new System.Diagnostics.Process()) {
                            generateFileProcess.StartInfo = new System.Diagnostics.ProcessStartInfo("\"C:\\Why\\3rd Party components\\FXC8\\fxc8.exe\"", arguments);
                            generateFileProcess.StartInfo.CreateNoWindow = true;
                            generateFileProcess.StartInfo.UseShellExecute = false;
                            generateFileProcess.StartInfo.RedirectStandardError = true;
                            generateFileProcess.StartInfo.RedirectStandardOutput = true;
                            generateFileProcess.Start();

                            generateFileProcess.WaitForExit();

                            if (generateFileProcess.ExitCode == 0) {
                                Stream resourceStream = File.Open(objectFileName, FileMode.Open, FileAccess.Read);
                                if (resourceStream != null) {
                                    loaded = true;

                                    try {
                                        byte[] compiledData = new byte[(int)resourceStream.Length];
                                        resourceStream.Read(compiledData, 0, compiledData.Length);

                                        using (DataStream dataStream = new DataStream(compiledData, true, false)) {
                                            ShaderBytecode = new SlimDX.D3DCompiler.ShaderBytecode(dataStream);
                                        }
                                    }
                                    finally {
                                        resourceStream.Dispose();
                                    }
                                } else {
                                    throw new Exception(string.Format("Error compiling shader: {0}", generateFileProcess.StandardError.ReadToEnd()));
                                }
                            } else {
                                //  If you want to retry with different shader source then
                                //  edit the shader now, save, and then skip back to the
                                //  start of the loop.

                                throw new Exception(string.Format("Error compiling shader: {0}", generateFileProcess.StandardError.ReadToEnd()));
                            }
                        }
                    }
                }

                //  If that didn't succeed then load the compiled
                //  object code from an embedded resource.
                if (!loaded) {
                    string embeddedResourceFileName = Path.ChangeExtension(filename, "txt");
                    Stream resourceStream = GetEmbeddedResourceFileStream(embeddedResourceFileName);

                    if (resourceStream != null) {
                        loaded = true;

                        try {
                            byte[] compiledData = ExtractCompiledEntryPoint(resourceStream, entryPoint);

                            if (compiledData != null) {
                                using (DataStream dataStream = new DataStream(compiledData, true, false)) {
                                    ShaderBytecode = new SlimDX.D3DCompiler.ShaderBytecode(dataStream);
                                }
                            }
                        }
                        finally {
                            resourceStream.Dispose();
                        }
                    }
                }

                //  If that didn't succeed then we need to error.
                if (!loaded) {
                    throw new CompilationException(string.Format("Can't find compiled shader {0}, entry point {1}.", filename, entryPoint));
                }



                //  We have compiled bytecode.
                //  Now turn that into a shader of the right type.

                try {
                    if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.ComputeShader)) {
                        Shader = new SlimDX.Direct3D11.ComputeShader(Device, ShaderBytecode) as ShaderType;
                    } else if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.GeometryShader)) {
                        Shader = new SlimDX.Direct3D11.GeometryShader(Device, ShaderBytecode) as ShaderType;
                    } else if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.VertexShader)) {
                        Shader = new SlimDX.Direct3D11.VertexShader(Device, ShaderBytecode) as ShaderType;
                    } else if (typeof(ShaderType) == typeof(SlimDX.Direct3D11.PixelShader)) {
                        Shader = new SlimDX.Direct3D11.PixelShader(Device, ShaderBytecode) as ShaderType;
                    }
                }
                catch (Exception e) {
                    throw new Exception(string.Format("Error creating shader {0}: {1}", entryPoint, e.Message), e);
                }

                ShaderSignature = SlimDX.D3DCompiler.ShaderSignature.GetInputSignature(ShaderBytecode);

                Shader.DebugName = Name;

                AnalyzeResourceBindings(ShaderBytecode);

                return;
            }
        }

        public static byte[] ExtractCompiledEntryPoint(Stream resourceStream, string entryPoint)
        {
            using (StreamReader reader = new StreamReader(resourceStream)) {
                string fileLine;
                while ((fileLine = reader.ReadLine()) != null) {
                    if (fileLine.StartsWith(entryPoint)) {
                        int quote1Index = fileLine.IndexOf('"', 0);
                        int quote2Index = fileLine.IndexOf('"', quote1Index + 1);
                        int quote3Index = fileLine.IndexOf('"', quote2Index + 1);
                        int quote4Index = fileLine.IndexOf('"', quote3Index + 1);
#if DEBUG
                        int startIndex = quote1Index + 1;
                        int finishIndex = quote2Index;
#else
                        int startIndex = quote3Index + 1;
                        int finishIndex = quote4Index;
#endif
                        int byteCount = (finishIndex - startIndex) / 2;
                        byte[] result = new byte[byteCount];
                        for (int i = 0; i < byteCount; ++i) {
                            char hiChar = fileLine[startIndex + i * 2 + 0];
                            char loChar = fileLine[startIndex + i * 2 + 1];
                            result[i] = ParseByteValue(hiChar, loChar);
                        }

                        return result;
                    }
                }
            }

            return null;
        }

        public static byte ParseByteValue(char hiChar, char loChar)
        {
            int hiValue;
            if (hiChar >= '0' && hiChar <= '9') {
                hiValue = (int)(hiChar - '0');
            } else {
                hiValue = (int)(hiChar - 'A') + 10;
            }

            int loValue;
            if (loChar >= '0' && loChar <= '9') {
                loValue = (int)(loChar - '0');
            } else {
                loValue = (int)(loChar - 'A') + 10;
            }

            return (byte)(hiValue * 16 + loValue);
        }

        public static string GetDebugResourceFileName(string fileName)
        {
#if DEBUG
            string debugResourcePath = Path.Combine(System.Environment.CurrentDirectory, "DebugResources");
            string debugResourceFileName = Path.Combine(debugResourcePath, fileName);
            if (!File.Exists(debugResourceFileName)) {
                return null;
            }

            return debugResourceFileName;
#else
            return null;
#endif
        }

        public static Stream GetEmbeddedResourceFileStream(string fileName)
        {
            string resourceName = string.Format("Why.DataFlowOperatorWork.FluidModelling.Shaders.{0}", fileName);

            return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void AnalyzeResourceBindings(SlimDX.D3DCompiler.ShaderBytecode shaderBytecode)
        {
            string disassembly = shaderBytecode.Disassemble(SlimDX.D3DCompiler.DisassemblyFlags.EnableDefaultValues);

            const string INPUT_SIGNATURE_LINE =             "// Resource Bindings:";
            const string EMPTY_LINE =                       "//";
            const string INPUT_SIGNATURE_HEADER_LINE =      "// Name                                 Type  Format         Dim Slot Elements";
            const string INPUT_SIGNATURE_SEPARATOR_LINE =   "// ------------------------------ ---------- ------- ----------- ---- --------";

            int index = disassembly.IndexOf(INPUT_SIGNATURE_LINE);
            if (index < 0) {
                return;
            }

            string line = ReadLine(disassembly, ref index);
            Debug.Assert(line == INPUT_SIGNATURE_LINE);

            line = ReadLine(disassembly, ref index);
            Debug.Assert(line == EMPTY_LINE);

            line = ReadLine(disassembly, ref index);
            Debug.Assert(line == INPUT_SIGNATURE_HEADER_LINE);

            line = ReadLine(disassembly, ref index);
            Debug.Assert(line == INPUT_SIGNATURE_SEPARATOR_LINE);

            line = ReadLine(disassembly, ref index);
            while (line != EMPTY_LINE) {
                string[] splits = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                string nameStr = splits[1];
                string typeStr = splits[2];
                string formatStr = splits[3];
                string dimStr = splits[4];
                string slotStr = splits[5];
                string elementsStr = splits[6];

                int slot = int.Parse(slotStr);

                if (typeStr == "cbuffer") {
                    m_constantBufferBindings[slot] = new ResourceBinding(slot, nameStr, 0);
                } else if (typeStr == "texture") {
                    m_shaderResourceViewBindings[slot] = new ResourceBinding(slot, nameStr, 0);
                } else if (typeStr == "UAV") {
                    m_unorderedAccessViewBindings[slot] = new ResourceBinding(slot, nameStr, 0);
                } else if (typeStr == "sampler") {
                } else {
                }

                line = ReadLine(disassembly, ref index);
            }
        }

        private static string ReadLine(string disassembly, ref int index)
        {
            int nextEOLOfs = disassembly.IndexOf('\n', index);
            if (nextEOLOfs < 0) {
                return null;
            }

            string line = disassembly.Substring(index, nextEOLOfs - index);
            index = nextEOLOfs + 1;

            return line;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal void CheckResourceBindings(SlimDX.Direct3D11.Buffer[] constantBuffers, SlimDX.Direct3D11.ShaderResourceView[] shaderResourceViews, SlimDX.Direct3D11.UnorderedAccessView[] unorderedAccessViews, bool allowUnusedResources)
        {
            for (int slot = 0; slot < 8; ++slot) {
                if (m_constantBufferBindings[slot] == null) {
                    if (constantBuffers != null && constantBuffers[slot] != null) {
                        string warningStr = string.Format("Warning: Shader {0} has constant buffer resource {1} mapped on slot {2} which is not referenced.", Name, constantBuffers[slot].DebugName, slot);
                        if (!allowUnusedResources) {
                            Debug.WriteLine(warningStr);
                        }
                    }
                } else {
                    if (constantBuffers == null || constantBuffers[slot] == null) {
                        string errorStr = string.Format("Error: Shader {0} references constant buffer resource {1} on slot {2} which is not mapped.", Name, m_constantBufferBindings[slot].Name, slot);
                        Debug.WriteLine(errorStr);
                        Debug.AssertFalse(errorStr);
                    }
                }

                if (m_shaderResourceViewBindings[slot] == null) {
                    if (shaderResourceViews != null && shaderResourceViews[slot] != null) {
                        string warningStr = string.Format("Warning: Shader {0} has SRV resource {1} mapped on slot {2} which is not referenced.", Name, shaderResourceViews[slot].DebugName, slot);
                        if (!allowUnusedResources) {
                            Debug.WriteLine(warningStr);
                        }
                    }
                } else {
                    if (shaderResourceViews == null || shaderResourceViews[slot] == null) {
                        string errorStr = string.Format("Error: Shader {0} references SRV resource {1} on slot {2} which is not mapped.", Name, m_shaderResourceViewBindings[slot].Name, slot);
                        Debug.WriteLine(errorStr);
                        Debug.AssertFalse(errorStr);
                    }
                }

                if (m_unorderedAccessViewBindings[slot] == null) {
                    if (unorderedAccessViews != null && unorderedAccessViews[slot] != null) {
                        string warningStr = string.Format("Warning: Shader {0} has UAV resource {1} mapped on slot {2} which is not referenced.", Name, unorderedAccessViews[slot].DebugName, slot);
                        if (!allowUnusedResources) {
                            Debug.WriteLine(warningStr);
                        }
                    }
                } else {
                    if (unorderedAccessViews == null || unorderedAccessViews[slot] == null) {
                        string errorStr = string.Format("Error: Shader {0} references UAV resource {1} on slot {2} which is not mapped.", Name, m_unorderedAccessViewBindings[slot].Name, slot);
                        Debug.WriteLine(errorStr);
                        Debug.AssertFalse(errorStr);
                    }
                }
            }
        }

        #endregion

        #region Private classes.

        private class ResourceBinding
        {
            public int Slot { get; private set; }
            public string Name { get; private set; }
            public int TypeSize { get; private set; }

            public ResourceBinding(int slot, string name, int typeSize)
            {
                Slot = slot;
                Name = name;
                TypeSize = typeSize;
            }
        }

        #endregion

        #region Disposal methods.

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                Shader = ResourceDisposer.DisposeResource(Shader);
                ShaderSignature = ResourceDisposer.DisposeResource(ShaderSignature);
                ShaderBytecode = ResourceDisposer.DisposeResource(ShaderBytecode);
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Private fields.

        private string m_debugName;

        private ResourceBinding[] m_constantBufferBindings = new ResourceBinding[8];
        private ResourceBinding[] m_shaderResourceViewBindings = new ResourceBinding[8];
        private ResourceBinding[] m_unorderedAccessViewBindings = new ResourceBinding[8];

        #endregion

    }

    public class GPUComputeShader : GPUShader<SlimDX.Direct3D11.ComputeShader>
    {

        #region Constructors.

        public GPUComputeShader(GPUShaderGroup shaderGroup, string filename, string entryPoint)
            : base(shaderGroup, filename, entryPoint)
        {
        }

        #endregion

        #region Public access methods.

        public void SetConstantBuffer(IConstantBuffer constantBuffer)
        {
            Debug.Assert(constantBuffer != null);
            SetConstantBuffer(constantBuffer, constantBuffer.DefaultSlot);
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView)
        {
            Debug.Assert(shaderResourceView != null);
            SetShaderResourceView(shaderResourceView, shaderResourceView.DefaultSlot);
        }

        public void SetUnorderedAccessView(IUnorderedAccessView unorderedAccessView)
        {
            Debug.Assert(unorderedAccessView != null);
            SetUnorderedAccessView(unorderedAccessView, unorderedAccessView.DefaultSlot);
        }

        public void SetConstantBuffer(IConstantBuffer constantBuffer, int slot)
        {
            Debug.Assert(constantBuffer != null);
            Debug.Assert(m_constantBuffers[slot] == null);
            m_constantBuffers[slot] = constantBuffer;
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView, int slot)
        {
            Debug.Assert(shaderResourceView != null);
            Debug.Assert(m_shaderResourceViews[slot] == null);
            m_shaderResourceViews[slot] = shaderResourceView;
        }

        public void SetUnorderedAccessView(IUnorderedAccessView unorderedAccessView, int slot)
        {
            Debug.Assert(unorderedAccessView != null);
            Debug.Assert(m_unorderedAccessViews[slot] == null);
            m_unorderedAccessViews[slot] = unorderedAccessView;
        }

        public void ClearConstantBuffer(int slot)
        {
            m_constantBuffers[slot] = null;
        }

        public void ClearShaderResourceView(int slot)
        {
            m_shaderResourceViews[slot] = null;
        }

        public void ClearUnorderedAccessView(int slot)
        {
            m_unorderedAccessViews[slot] = null;
        }

        public void Dispatch(int threadGroupCountX)
        {
            Dispatch(threadGroupCountX, 1, 1);
        }

        public void Dispatch(int threadGroupCountX, int threadGroupCountY)
        {
            Dispatch(threadGroupCountX, threadGroupCountY, 1);
        }

        public void Dispatch(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
        {
            SetResources();

            CheckResourceBindings(Device.ImmediateContext.ComputeShader.GetConstantBuffers(0, 8), Device.ImmediateContext.ComputeShader.GetShaderResources(0, 8), Device.ImmediateContext.ComputeShader.GetUnorderedAccessViews(0, 8), false);

            Device.ImmediateContext.Dispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ);

            //Device.ImmediateContext.Flush();

            ClearResources();
        }

        public void DispatchNative(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ, SlimDX.Direct3D11.ShaderResourceView[] shaderResourceViews, int[] srvSlots, SlimDX.Direct3D11.UnorderedAccessView[] unorderedAccessViews, int[] uavSlots)
        {
            Debug.Assert(shaderResourceViews != null);
            Debug.Assert(srvSlots != null);
            Debug.Assert(shaderResourceViews.Length == srvSlots.Length);
            Debug.Assert(unorderedAccessViews != null);
            Debug.Assert(uavSlots != null);
            Debug.Assert(unorderedAccessViews.Length == uavSlots.Length);

            SetResources();

            for (int srvNum = 0; srvNum < shaderResourceViews.Length; ++srvNum) {
                SlimDX.Direct3D11.ShaderResourceView shaderResourceView = shaderResourceViews[srvNum];
                Debug.Assert(shaderResourceView != null);
                int slot = srvSlots[srvNum];

                Debug.Assert(m_shaderResourceViews[slot] == null);
                Device.ImmediateContext.ComputeShader.SetShaderResource(shaderResourceView, slot);
            }

            for (int uavNum = 0; uavNum < unorderedAccessViews.Length; ++uavNum) {
                SlimDX.Direct3D11.UnorderedAccessView unorderedAccessView = unorderedAccessViews[uavNum];
                Debug.Assert(unorderedAccessView != null);
                int slot = uavSlots[uavNum];

                Debug.Assert(m_unorderedAccessViews[slot] == null);
                Device.ImmediateContext.ComputeShader.SetUnorderedAccessView(unorderedAccessView, slot);
            }

            CheckResourceBindings(Device.ImmediateContext.ComputeShader.GetConstantBuffers(0, 8), Device.ImmediateContext.ComputeShader.GetShaderResources(0, 8), Device.ImmediateContext.ComputeShader.GetUnorderedAccessViews(0, 8), false);

            Device.ImmediateContext.Dispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ);

            ClearResources();
        }

        #endregion

        #region Private methods.

        private void SetResources()
        {
            Device.ImmediateContext.ComputeShader.Set(Shader);

            for (int slot = 0; slot < 8; ++slot) {
                Device.ImmediateContext.ComputeShader.SetConstantBuffer(m_constantBuffers[slot] == null ? null : m_constantBuffers[slot].Buffer, slot);
                Device.ImmediateContext.ComputeShader.SetShaderResource(m_shaderResourceViews[slot] == null ? null : m_shaderResourceViews[slot].ShaderResourceView, slot);
                Device.ImmediateContext.ComputeShader.SetUnorderedAccessView(m_unorderedAccessViews[slot] == null ? null : m_unorderedAccessViews[slot].UnorderedAccessView, slot);
            }
        }

        private void ClearResources()
        {
            Device.ImmediateContext.ComputeShader.SetConstantBuffers(m_nullConstantBuffers, 0, m_nullConstantBuffers.Length);
            Device.ImmediateContext.ComputeShader.SetShaderResources(m_nullShaderResourceViews, 0, m_nullShaderResourceViews.Length);
            Device.ImmediateContext.ComputeShader.SetUnorderedAccessViews(m_nullUnorderedAccessViews, 0, m_nullUnorderedAccessViews.Length);
            Device.ImmediateContext.ComputeShader.Set(null);
        }

        #endregion

        #region Private fields.

        private IConstantBuffer[] m_constantBuffers = new IConstantBuffer[8];
        private IShaderResourceView[] m_shaderResourceViews = new IShaderResourceView[8];
        private IUnorderedAccessView[] m_unorderedAccessViews = new IUnorderedAccessView[8];

        private SlimDX.Direct3D11.Buffer[] m_nullConstantBuffers = new SlimDX.Direct3D11.Buffer[8] { null, null, null, null, null, null, null, null };
        private SlimDX.Direct3D11.ShaderResourceView[] m_nullShaderResourceViews = new SlimDX.Direct3D11.ShaderResourceView[8] { null, null, null, null, null, null, null, null };
        private SlimDX.Direct3D11.UnorderedAccessView[] m_nullUnorderedAccessViews = new SlimDX.Direct3D11.UnorderedAccessView[8] { null, null, null, null, null, null, null, null };

        #endregion

    }

    public class GPUVertexShader : GPUShader<SlimDX.Direct3D11.VertexShader>
    {

        #region Constructors.

        public GPUVertexShader(GPUShaderGroup shaderGroup, string filename, string entryPoint)
            : base(shaderGroup, filename, entryPoint)
        {
        }

        #endregion

        #region Public access methods.

        public void SetConstantBuffer(IConstantBuffer constantBuffer)
        {
            Debug.Assert(constantBuffer != null);
            SetConstantBuffer(constantBuffer, constantBuffer.DefaultSlot);
        }

        public void SetConstantBuffer(IConstantBuffer constantBuffer, int slot)
        {
            Debug.Assert(constantBuffer != null);
            Debug.Assert(m_constantBuffers[slot] == null);
            m_constantBuffers[slot] = constantBuffer;
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView)
        {
            Debug.Assert(shaderResourceView != null);
            SetShaderResourceView(shaderResourceView, shaderResourceView.DefaultSlot);
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView, int slot)
        {
            Debug.Assert(shaderResourceView != null);
            Debug.Assert(m_shaderResourceViews[slot] == null);
            m_shaderResourceViews[slot] = shaderResourceView;
        }

        public void ClearConstantBuffer(int slot)
        {
            m_constantBuffers[slot] = null;
        }

        public void ClearShaderResourceView(int slot)
        {
            m_shaderResourceViews[slot] = null;
        }

        #endregion

        #region Private methods.

        internal void SetResources()
        {
            Device.ImmediateContext.VertexShader.Set(Shader);

            for (int slot = 0; slot < 8; ++slot) {
                Device.ImmediateContext.VertexShader.SetConstantBuffer(m_constantBuffers[slot] == null ? null : m_constantBuffers[slot].Buffer, slot);
                Device.ImmediateContext.VertexShader.SetShaderResource(m_shaderResourceViews[slot] == null ? null : m_shaderResourceViews[slot].ShaderResourceView, slot);
            }
        }

        internal void ClearResources()
        {
            Device.ImmediateContext.VertexShader.SetConstantBuffers(m_nullConstantBuffers, 0, m_nullConstantBuffers.Length);
            Device.ImmediateContext.VertexShader.SetShaderResources(m_nullShaderResourceViews, 0, m_nullShaderResourceViews.Length);
            Device.ImmediateContext.VertexShader.Set(null);
        }

        #endregion

        #region Private fields.

        private IConstantBuffer[] m_constantBuffers = new IConstantBuffer[8];
        private IShaderResourceView[] m_shaderResourceViews = new IShaderResourceView[8];

        private SlimDX.Direct3D11.Buffer[] m_nullConstantBuffers = new SlimDX.Direct3D11.Buffer[8] { null, null, null, null, null, null, null, null };
        private SlimDX.Direct3D11.ShaderResourceView[] m_nullShaderResourceViews = new SlimDX.Direct3D11.ShaderResourceView[8] { null, null, null, null, null, null, null, null };

        #endregion

    }

    public class GPUGeometryShader : GPUShader<SlimDX.Direct3D11.GeometryShader>
    {

        #region Constructors.

        public GPUGeometryShader(GPUShaderGroup shaderGroup, string filename, string entryPoint)
            : base(shaderGroup, filename, entryPoint)
        {
        }

        #endregion

        #region Public access methods.

        public void SetConstantBuffer(IConstantBuffer constantBuffer)
        {
            Debug.Assert(constantBuffer != null);
            SetConstantBuffer(constantBuffer, constantBuffer.DefaultSlot);
        }

        public void SetConstantBuffer(IConstantBuffer constantBuffer, int slot)
        {
            Debug.Assert(constantBuffer != null);
            Debug.Assert(m_constantBuffers[slot] == null);
            m_constantBuffers[slot] = constantBuffer;
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView)
        {
            Debug.Assert(shaderResourceView != null);
            SetShaderResourceView(shaderResourceView, shaderResourceView.DefaultSlot);
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView, int slot)
        {
            Debug.Assert(shaderResourceView != null);
            Debug.Assert(m_shaderResourceViews[slot] == null);
            m_shaderResourceViews[slot] = shaderResourceView;
        }

        public void ClearConstantBuffer(int slot)
        {
            m_constantBuffers[slot] = null;
        }

        public void ClearShaderResourceView(int slot)
        {
            m_shaderResourceViews[slot] = null;
        }

        #endregion

        #region Private methods.

        internal void SetResources()
        {
            Device.ImmediateContext.GeometryShader.Set(Shader);

            for (int slot = 0; slot < 8; ++slot) {
                Device.ImmediateContext.GeometryShader.SetConstantBuffer(m_constantBuffers[slot] == null ? null : m_constantBuffers[slot].Buffer, slot);
                Device.ImmediateContext.GeometryShader.SetShaderResource(m_shaderResourceViews[slot] == null ? null : m_shaderResourceViews[slot].ShaderResourceView, slot);
            }
        }

        internal void ClearResources()
        {
            Device.ImmediateContext.GeometryShader.SetConstantBuffers(m_nullConstantBuffers, 0, m_nullConstantBuffers.Length);
            Device.ImmediateContext.GeometryShader.SetShaderResources(m_nullShaderResourceViews, 0, m_nullShaderResourceViews.Length);
            Device.ImmediateContext.GeometryShader.Set(null);
        }

        #endregion

        #region Private fields.

        private IConstantBuffer[] m_constantBuffers = new IConstantBuffer[8];
        private IShaderResourceView[] m_shaderResourceViews = new IShaderResourceView[8];

        private SlimDX.Direct3D11.Buffer[] m_nullConstantBuffers = new SlimDX.Direct3D11.Buffer[8] { null, null, null, null, null, null, null, null };
        private SlimDX.Direct3D11.ShaderResourceView[] m_nullShaderResourceViews = new SlimDX.Direct3D11.ShaderResourceView[8] { null, null, null, null, null, null, null, null };

        #endregion

    }

    public class GPUPixelShader : GPUShader<SlimDX.Direct3D11.PixelShader>
    {

        #region Constructors.

        public GPUPixelShader(GPUShaderGroup shaderGroup, string filename, string entryPoint)
            : base(shaderGroup, filename, entryPoint)
        {
        }

        #endregion

        #region Public access methods.

        public void SetConstantBuffer(IConstantBuffer constantBuffer)
        {
            Debug.Assert(constantBuffer != null);
            SetConstantBuffer(constantBuffer, constantBuffer.DefaultSlot);
        }

        public void SetConstantBuffer(IConstantBuffer constantBuffer, int slot)
        {
            Debug.Assert(constantBuffer != null);
            Debug.Assert(m_constantBuffers[slot] == null);
            m_constantBuffers[slot] = constantBuffer;
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView)
        {
            Debug.Assert(shaderResourceView != null);
            SetShaderResourceView(shaderResourceView, shaderResourceView.DefaultSlot);
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView, int slot)
        {
            Debug.Assert(shaderResourceView != null);
            Debug.Assert(m_shaderResourceViews[slot] == null);
            m_shaderResourceViews[slot] = shaderResourceView;
        }

        public void ClearConstantBuffer(int slot)
        {
            m_constantBuffers[slot] = null;
        }

        public void ClearShaderResourceView(int slot)
        {
            m_shaderResourceViews[slot] = null;
        }

        #endregion

        #region Private methods.

        internal void SetResources()
        {
            Device.ImmediateContext.PixelShader.Set(Shader);

            for (int slot = 0; slot < 8; ++slot) {
                Device.ImmediateContext.PixelShader.SetConstantBuffer(m_constantBuffers[slot] == null ? null : m_constantBuffers[slot].Buffer, slot);
                Device.ImmediateContext.PixelShader.SetShaderResource(m_shaderResourceViews[slot] == null ? null : m_shaderResourceViews[slot].ShaderResourceView, slot);
            }
        }

        internal void ClearResources()
        {
            Device.ImmediateContext.PixelShader.SetConstantBuffers(m_nullConstantBuffers, 0, m_nullConstantBuffers.Length);
            Device.ImmediateContext.PixelShader.SetShaderResources(m_nullShaderResourceViews, 0, m_nullShaderResourceViews.Length);
            Device.ImmediateContext.PixelShader.Set(null);
        }

        #endregion

        #region Private fields.

        private IConstantBuffer[] m_constantBuffers = new IConstantBuffer[8];
        private IShaderResourceView[] m_shaderResourceViews = new IShaderResourceView[8];

        private SlimDX.Direct3D11.Buffer[] m_nullConstantBuffers = new SlimDX.Direct3D11.Buffer[8] { null, null, null, null, null, null, null, null };
        private SlimDX.Direct3D11.ShaderResourceView[] m_nullShaderResourceViews = new SlimDX.Direct3D11.ShaderResourceView[8] { null, null, null, null, null, null, null, null };

        #endregion

    }



    public class GPURenderShaders : IDisposable
    {

        #region Public properties.

        public GPUShaderGroup ShaderGroup { get; private set; }
        public SlimDX.Direct3D11.Device Device { get { return ShaderGroup.Device; } }

        public GPUVertexShader VertexShader { get; private set; }
        public GPUGeometryShader GeometryShader { get; private set; }
        public GPUPixelShader PixelShader { get; private set; }

        public SlimDX.Direct3D11.InputLayout InputLayout { get { EnsureInputLayoutBuilt(); return m_inputLayout; } }
        public SlimDX.Direct3D11.PrimitiveTopology PrimitiveTopology { get; private set; }

        #endregion

        #region Constructors.

        public GPURenderShaders(GPUShaderGroup shaderGroup, string filename, string vertexShaderEntryPoint, string geometryShaderEntryPoint, string pixelShaderEntryPoint)
        {
            ShaderGroup = shaderGroup;

            VertexShader = new GPUVertexShader(shaderGroup, filename, vertexShaderEntryPoint);
            GeometryShader = new GPUGeometryShader(shaderGroup, filename, geometryShaderEntryPoint);
            PixelShader = new GPUPixelShader(shaderGroup, filename, pixelShaderEntryPoint);

            if (shaderGroup != null) {
                shaderGroup.AddRenderShaders(this);
            }
        }

        #endregion

        #region Public access methods.

        public void SetPrimitiveTopology(SlimDX.Direct3D11.PrimitiveTopology primitiveTopology)
        {
            PrimitiveTopology = primitiveTopology;
        }

        public void SetInputLayout(SlimDX.Direct3D11.InputElement[] inputElements)
        {
            Debug.Assert(m_inputElements == null);
            m_inputElements = inputElements;
        }

        public void SetVertexBuffer(VertexBuffer vertexBuffer)
        {
            Debug.Assert(vertexBuffer != null);
            SetVertexBuffer(vertexBuffer, vertexBuffer.DefaultSlot);
        }

        public void SetVertexBuffer(VertexBuffer vertexBuffer, int slot)
        {
            Debug.Assert(vertexBuffer != null);
            Debug.Assert(m_vertexBuffers[slot] == null);
            m_vertexBuffers[slot] = vertexBuffer;
        }

        public void SetIndexBuffer(IndexBuffer indexBuffer)
        {
            Debug.Assert(indexBuffer != null);
            Debug.Assert(m_indexBuffer == null);
            m_indexBuffer = indexBuffer;
        }

        public void SetConstantBuffer(IConstantBuffer constantBuffer)
        {
            VertexShader.SetConstantBuffer(constantBuffer);
            GeometryShader.SetConstantBuffer(constantBuffer);
            PixelShader.SetConstantBuffer(constantBuffer);
        }

        public void SetShaderResourceView(IShaderResourceView shaderResourceView)
        {
            VertexShader.SetShaderResourceView(shaderResourceView);
            GeometryShader.SetShaderResourceView(shaderResourceView);
            PixelShader.SetShaderResourceView(shaderResourceView);
        }

        private void SetConstantBuffer(IConstantBuffer constantBuffer, int slot)
        {
            VertexShader.SetConstantBuffer(constantBuffer, slot);
            GeometryShader.SetConstantBuffer(constantBuffer, slot);
            PixelShader.SetConstantBuffer(constantBuffer, slot);
        }

        private void SetShaderResourceView(IShaderResourceView shaderResourceView, int slot)
        {
            VertexShader.SetShaderResourceView(shaderResourceView, slot);
            GeometryShader.SetShaderResourceView(shaderResourceView, slot);
            PixelShader.SetShaderResourceView(shaderResourceView, slot);
        }

        public void ClearVertexBuffer(int slot)
        {
            m_vertexBuffers[slot] = null;
        }

        public void ClearIndexBuffer()
        {
            m_indexBuffer = null;
        }

        public void ClearConstantBuffer(int slot)
        {
            VertexShader.ClearConstantBuffer(slot);
            GeometryShader.ClearConstantBuffer(slot);
            PixelShader.ClearConstantBuffer(slot);
        }

        public void ClearShaderResourceView(int slot)
        {
            VertexShader.ClearShaderResourceView(slot);
            GeometryShader.ClearShaderResourceView(slot);
            PixelShader.ClearShaderResourceView(slot);
        }

        public void Draw(int vertexCount, int startVertexLocation)
        {
            SetResources();

            VertexShader.CheckResourceBindings(Device.ImmediateContext.VertexShader.GetConstantBuffers(0, 8), Device.ImmediateContext.VertexShader.GetShaderResources(0, 8), null, true);
            GeometryShader.CheckResourceBindings(Device.ImmediateContext.GeometryShader.GetConstantBuffers(0, 8), Device.ImmediateContext.GeometryShader.GetShaderResources(0, 8), null, true);
            PixelShader.CheckResourceBindings(Device.ImmediateContext.PixelShader.GetConstantBuffers(0, 8), Device.ImmediateContext.PixelShader.GetShaderResources(0, 8), null, true);

            Device.ImmediateContext.Draw(vertexCount, startVertexLocation);

            ClearResources();
        }

        public void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            SetResources();

            VertexShader.CheckResourceBindings(Device.ImmediateContext.VertexShader.GetConstantBuffers(0, 8), Device.ImmediateContext.VertexShader.GetShaderResources(0, 8), null, true);
            GeometryShader.CheckResourceBindings(Device.ImmediateContext.GeometryShader.GetConstantBuffers(0, 8), Device.ImmediateContext.GeometryShader.GetShaderResources(0, 8), null, true);
            PixelShader.CheckResourceBindings(Device.ImmediateContext.PixelShader.GetConstantBuffers(0, 8), Device.ImmediateContext.PixelShader.GetShaderResources(0, 8), null, true);

            Device.ImmediateContext.DrawIndexed(indexCount, startIndexLocation, baseVertexLocation);

            ClearResources();
        }

        public void Dispose()
        {
            if (ShaderGroup == null) {
                VertexShader = ResourceDisposer.DisposeResource(VertexShader);
                GeometryShader = ResourceDisposer.DisposeResource(GeometryShader);
                PixelShader = ResourceDisposer.DisposeResource(PixelShader);
            } else {
                VertexShader = null;
                GeometryShader = null;
                PixelShader = null;
                ShaderGroup = null;
            }

            m_inputLayout = ResourceDisposer.DisposeResource(m_inputLayout);
        }

        #endregion

        #region Private methods.

        private void EnsureInputLayoutBuilt()
        {
            if (m_inputElements == null || m_inputLayout != null) {
                return;
            }

            m_inputLayout = new SlimDX.Direct3D11.InputLayout(Device, VertexShader.ShaderSignature, m_inputElements);
        }

        private void SetResources()
        {
            VertexShader.SetResources();
            GeometryShader.SetResources();
            PixelShader.SetResources();

            Device.ImmediateContext.InputAssembler.InputLayout = InputLayout;
            Device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology;
            Device.ImmediateContext.InputAssembler.SetIndexBuffer((m_indexBuffer == null) ? null : m_indexBuffer.Buffer, SlimDX.DXGI.Format.R32_UInt, 0);

            for (int slot = 0; slot < 8; ++slot) {
                if (m_vertexBuffers[slot] == null) {
                    Device.ImmediateContext.InputAssembler.SetVertexBuffers(slot, new SlimDX.Direct3D11.VertexBufferBinding(null, 0, 0));
                } else {
                    Device.ImmediateContext.InputAssembler.SetVertexBuffers(slot, new SlimDX.Direct3D11.VertexBufferBinding(m_vertexBuffers[slot].Buffer, m_vertexBuffers[slot].GetTypeSize(), 0));
                }
            }
        }

        private void ClearResources()
        {
            Device.ImmediateContext.InputAssembler.InputLayout = null;
            Device.ImmediateContext.InputAssembler.PrimitiveTopology = SlimDX.Direct3D11.PrimitiveTopology.PointList;
            Device.ImmediateContext.InputAssembler.SetIndexBuffer(null, SlimDX.DXGI.Format.R32_UInt, 0);
            Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, m_nullVertexBuffers);

            VertexShader.ClearResources();
            GeometryShader.ClearResources();
            PixelShader.ClearResources();
        }

        #endregion

        #region Private fields.

        private SlimDX.Direct3D11.InputLayout m_inputLayout;
        private SlimDX.Direct3D11.InputElement[] m_inputElements;

        private VertexBuffer[] m_vertexBuffers = new VertexBuffer[8];
        private IndexBuffer m_indexBuffer = null;

        private SlimDX.Direct3D11.VertexBufferBinding[] m_nullVertexBuffers = new SlimDX.Direct3D11.VertexBufferBinding[8];

        #endregion

    }

}
