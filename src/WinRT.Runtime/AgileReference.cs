﻿using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace WinRT
{
#if EMBED
    internal
#else
    public
#endif 
    class AgileReference : IDisposable
    {
        private readonly static Guid CLSID_StdGlobalInterfaceTable = Guid.Parse("00000323-0000-0000-c000-000000000046");
        private readonly static Lazy<IGlobalInterfaceTable> Git = new Lazy<IGlobalInterfaceTable>(() => GetGitTable());
        private readonly IAgileReference _agileReference;
        private readonly IntPtr _cookie;
        private bool disposed;

 #if EMBED
        protected unsafe AgileReference(IObjectReference instance)
#else
        public unsafe AgileReference(IObjectReference instance)
#endif 
        
        {
            if(instance?.ThisPtr == null)
            {
                return;
            }   

            IntPtr agileReference = default;
            Guid iid = typeof(IUnknownVftbl).GUID;
            try
            {
                Marshal.ThrowExceptionForHR(Platform.RoGetAgileReference(
                    0 /*AGILEREFERENCE_DEFAULT*/,
                    ref iid,
                    instance.ThisPtr,
                    &agileReference));
#if NET5_0
                _agileReference = (IAgileReference)new SingleInterfaceOptimizedObject(typeof(IAgileReference), ObjectReference<ABI.WinRT.Interop.IAgileReference.Vftbl>.Attach(ref agileReference));
#else
                _agileReference = ABI.WinRT.Interop.IAgileReference.FromAbi(agileReference).AsType<ABI.WinRT.Interop.IAgileReference>();
#endif
            }
            catch(TypeLoadException)
            {
                _cookie = Git.Value.RegisterInterfaceInGlobal(instance, iid);
            }
            finally
            {
                MarshalInterface<IAgileReference>.DisposeAbi(agileReference);
            }
        }

#if EMBED
        protected
#else
        public
#endif
        IObjectReference Get() => _cookie == IntPtr.Zero ? _agileReference?.Resolve(typeof(IUnknownVftbl).GUID) : Git.Value?.GetInterfaceFromGlobal(_cookie, typeof(IUnknownVftbl).GUID);

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (_cookie != IntPtr.Zero)
                {
                    Git.Value.RevokeInterfaceFromGlobal(_cookie);
                }
                disposed = true;
            }
        }

        private static unsafe IGlobalInterfaceTable GetGitTable()
        {
            Guid gitClsid = CLSID_StdGlobalInterfaceTable;
            Guid gitIid = typeof(IGlobalInterfaceTable).GUID;
            IntPtr gitPtr = default;

            try
            {
                Marshal.ThrowExceptionForHR(Platform.CoCreateInstance(
                    ref gitClsid,
                    IntPtr.Zero,
                    1 /*CLSCTX_INPROC_SERVER*/,
                    ref gitIid,
                    &gitPtr));
                return ABI.WinRT.Interop.IGlobalInterfaceTable.FromAbi(gitPtr).AsType<ABI.WinRT.Interop.IGlobalInterfaceTable>();
            }
            finally
            {
                MarshalInterface<IGlobalInterfaceTable>.DisposeAbi(gitPtr);
            }
        }

        ~AgileReference()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

#if EMBED
    internal
#else
    public 
#endif
    sealed class AgileReference<T> : AgileReference
        where T : class
    {

#if EMBED
        protected
#else
        public
#endif
        unsafe AgileReference(IObjectReference instance)
            : base(instance)
        {
        }

#if EMBED
        protected
#else
        public
#endif
        new T Get() 
        {
            using var objRef = base.Get();
            return ComWrappersSupport.CreateRcwForComObject<T>(objRef?.ThisPtr ?? IntPtr.Zero);
        }
    }
}