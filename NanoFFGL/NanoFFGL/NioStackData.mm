#import "Foundation/Foundation.h"

#include <algorithm>
#include <memory>
#include <string>
#include <stdexcept>
#include <map>
#include <unordered_map>

#include "NioStackData.h"


struct NioStackData {
  std::vector<NioStackEntry> Entries;
};

static NSLock* g_threadMapLock = nullptr;
static std::map<NSThread*, NioStackData*>* g_threadMap = nullptr;

extern "C" NioStackDataHandle NioGetStackData() {
  NSThread* thread = [NSThread currentThread];
  [g_threadMapLock lock];
  NioStackData* data;
  auto findIt = g_threadMap->find(thread);
  if (findIt == g_threadMap->end()) {
    data = new NioStackData();
    (*g_threadMap)[thread] = data;
  } else {
    data = findIt->second;
  }
  [g_threadMapLock unlock];
  return data;
}

extern "C" int32_t NioGetStackDataEntryCount(NioStackDataHandle handle) {
  NioStackData* data = (NioStackData*)handle;
  return (int32_t)data->Entries.size();
}

extern "C" NioStackEntry* NioGetStackDataEntry(NioStackDataHandle handle, int32_t index) {
  NioStackData* data = (NioStackData*)handle;
  return &data->Entries[index];
}

extern "C" NioStackEntry* NioAddStackDataEntry(NioStackDataHandle handle) {
  NioStackData* data = (NioStackData*)handle;
  return &data->Entries.emplace_back();
}

extern "C" void NioRemoveStackDataEntry(NioStackDataHandle handle, int32_t index) {
  NioStackData* data = (NioStackData*)handle;
  data->Entries.erase(data->Entries.begin() + index);
}


extern "C" void __attribute__((constructor)) DylibConstructor() {
  g_threadMapLock = [[NSLock alloc] init];
  g_threadMap = new std::map<NSThread*, NioStackData*>();
}

extern "C" void __attribute__((destructor)) DylibDestructor() {
  g_threadMapLock = nullptr;
  delete g_threadMap;
  g_threadMap = nullptr;
}


