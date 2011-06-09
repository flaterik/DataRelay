#pragma once
#include "Stdafx.h"
#include "Alloc.h"


#ifndef INT32_MAX
#define INT32_MAX (~(1 << ((8 * sizeof(__int32)) - 1)))
#endif

namespace BerkeleyDbWrapper
{
	using namespace std;
	using namespace System;
	using namespace System::Runtime::InteropServices;
	using namespace MySpace::Common::Storage;


	ref class MemoryUtil {
	public:
		static PostAccessUnmanagedMemoryCleanup^ AllocClean;
		static MemoryUtil() {
			AllocClean = gcnew PostAccessUnmanagedMemoryCleanup(&free_wrapper);
		}
	};

	struct DbtExtended : Dbt {
		DbtExtended() : Dbt() {
		}
		void set_for_partial(__int32 offset, __int32 length) {
			set_doff(offset < 0 ? 0 : offset);
			set_dlen(length < 0 ? INT32_MAX : length);
			set_flags(get_flags() | DB_DBT_PARTIAL);
		}
		SafeUnmanagedMemoryStream ^CreateStream() {
			return gcnew SafeUnmanagedMemoryStream((Byte *)get_data(), get_size(),
				MemoryUtil::AllocClean);
		}
		array<Byte> ^CreateBuffer() {
			__int32 size = get_size();
			if (size < 0) return nullptr;
			if (size == 0) return gcnew array<Byte>(0);
			void *ptr = safe_cast<DBT *>(this)->app_data;
			if (ptr == NULL) return nullptr;
			GCHandle handle = GCHandle::FromIntPtr(IntPtr(ptr));
			try {
				return safe_cast<array<Byte>^>(handle.Target);
			} finally {
				handle.Free();
			}
		}
	};

	struct DbtHolder : DbtExtended {
		DbtHolder() : DbtExtended(), _initialized(false) {
			memset(&_handle, 0, sizeof(GCHandle));
		}
		~DbtHolder() {
			if (_handle.IsAllocated) {
				_handle.Free();
			}
		}
		void initialize_for_read(DataBuffer &bf) {
			if (_initialized) {
				throw gcnew ApplicationException("Buffer already initialized");
			}
			_initialized = true;
			if (bf.IsObject) {
				if (_handle.IsAllocated) {
					throw new exception("Attempt to set object on DbtHolder with object already set");
				}
				Int32 offset, length;
				Object ^o = bf.GetObjectValue(offset, length);
				_handle = GCHandle::Alloc(o, GCHandleType::Pinned);
				set_data(static_cast<Byte *>(_handle.AddrOfPinnedObject().ToPointer())
					+ offset);
				set_size(length);
			}
			else {
				switch(bf.Type) {
					case DataBufferType::Empty:
						set_data(NULL);
						set_size(0);
						break;
					case DataBufferType::Int32:
						_data._valueInt32 = bf.Int32Value;
						set_data(&_data._valueInt32);
						set_size(4);
						break;
					case DataBufferType::Int64:
						_data._valueInt64 = bf.Int64Value;
						set_data(&_data._valueInt64);
						set_size(8);
						break;
					default:
						throw gcnew ApplicationException(L"Unhandled buffer type " +
							bf.Type.ToString());
				}
			}
		}
		void initialize_for_read_write(DataBuffer &bf) {
			initialize_for_read(bf);
			set_ulen(get_size());
			set_flags(get_flags() | DB_DBT_USERMEM);		
		}
		void initialize_for_write(DataBuffer &bf) {
			if (!(bf.IsWritable || bf.IsEmpty)) {
				throw gcnew ApplicationException("Buffer isn't writable");
			}
			initialize_for_read_write(bf);
		}
	private:
		bool _initialized;
		union {
			__int32 _valueInt32;
			__int64 _valueInt64;
		} _data;
		GCHandle _handle;
	};
}