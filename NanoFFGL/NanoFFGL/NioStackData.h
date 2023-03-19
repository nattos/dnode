
#pragma once

#include <vector>

struct NioStackEntry {
  void* Source;
  int LastFrameNumber;
  NSObject* TextureRef;
  uint32_t OpenGLTexture;
};

typedef void* NioStackDataHandle;


typedef NioStackDataHandle(*NioGetStackDataPtr)(void);
typedef int32_t(*NioGetStackDataEntryCountPtr)(NioStackDataHandle);
typedef NioStackEntry*(*NioGetStackDataEntryPtr)(NioStackDataHandle, int32_t);
typedef NioStackEntry*(*NioAddStackDataEntryPtr)(NioStackDataHandle);
typedef void(*NioRemoveStackDataEntryPtr)(NioStackDataHandle, int32_t);


//
//extern "C" NioStackDataHandle NioGetStackData();
