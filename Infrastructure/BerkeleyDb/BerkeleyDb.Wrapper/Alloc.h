#pragma once
#include "stdafx.h"

#if (defined(_DEBUG) || defined(USE_EXPLICIT_ALLOC)) && !defined(DONT_USE_EXPLICIT_ALLOC)

inline void *malloc_wrapper(size_t size) { return malloc(size); }
inline void *realloc_wrapper(void *p, size_t size) { return realloc(p, size); }
inline void free_wrapper(void *p) { return free(p); }

#else

#define malloc_wrapper malloc
#define realloc_wrapper realloc
#define free_wrapper free

#endif
