//
//  ofxPONK.h
//
//
//  Created by Tim Redfern March 2023
//
//  PONK (Pathes Over NetworK) is a minimal protocol to transfer 2D colored pathes from a source to
//  a receiver. It has been developped to transfer laser path from a software to another over network using UDP.
//
//  https://github.com/madmappersoftware/Ponk
//

#pragma once

#include <string>

#include "PonkDefs.h"

extern std::string ipv4_int_to_string(uint32_t in, bool *const success=nullptr);
extern uint32_t ipv4_string_to_int(const std::string &in, bool *const success=nullptr);

struct GenericAddr;
class DatagramSocket;

struct PonkSenderPoint {
  // In range [-1.0, 1.0].
  vector_float2 Point;
  // In range [0.0, 1.0].
  vector_float4 Color;
};

class PONKSender {
public:
  PONKSender(unsigned int ip = ((127 << 24) + (0 << 16) + (0 << 8) + 1), unsigned short port = PONK_PORT);
  ~PONKSender();
  int draw(const std::vector<std::vector<PonkSenderPoint>>& lines, int intensity=255);

private:
  std::unique_ptr<GenericAddr> dest;
  std::unique_ptr<DatagramSocket> socket;
};
