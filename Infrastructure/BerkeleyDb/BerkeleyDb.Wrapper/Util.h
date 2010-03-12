#pragma once

using namespace System;
using namespace System::Runtime::InteropServices;

namespace BerkeleyDbWrapper
{
	class CStr
	{
	public:
		CStr(String ^s)
		{
			m_ptr = Marshal::StringToHGlobalAnsi(s);
		}

		~CStr()
		{
			if (m_ptr != IntPtr::Zero)
			{
				Marshal::FreeHGlobal(m_ptr);
				m_ptr = IntPtr::Zero;
			}
		}

		const char *c_str()
		{
			return reinterpret_cast<const char*>(m_ptr.ToPointer());
		}

	private:
		IntPtr m_ptr;

		// These two disallow reassignment
		CStr(const CStr&);
		CStr& operator=(const CStr&);
	};
}