#include "Prefix.pch"

#include <arpa/inet.h> // inet_ntop & inet_pton
#include <string.h> // strerror_r
#include <arpa/inet.h> // ntohl & htonl
#include <simd/simd.h>

#include "PonkSender.h"
#include "DatagramSocket.h"

PONKSender::PONKSender(unsigned int ip, unsigned short port) {
  socket.reset(new DatagramSocket(INADDR_ANY, 0));
  dest.reset(new GenericAddr());
  dest->family = AF_INET;
  dest->ip = ip;
  dest->port = port;
//  ofLog()<<"PONKSender "<<ipv4_int_to_string(ip)<<":"<<ofToString(port);
}

// Required because of forward declared prototypes.
PONKSender::~PONKSender(){}






namespace {
void push8bits(std::vector<unsigned char>& fullData, unsigned char value) {
    fullData.push_back(value);
}

void push16bits(std::vector<unsigned char>& fullData, unsigned short value) {
    fullData.push_back(static_cast<unsigned char>((value>>0) & 0xFF));
    fullData.push_back(static_cast<unsigned char>((value>>8) & 0xFF));
}

void push32bits(std::vector<unsigned char>& fullData, int value) {
    fullData.push_back(static_cast<unsigned char>((value>>0) & 0xFF));
    fullData.push_back(static_cast<unsigned char>((value>>8) & 0xFF));
    fullData.push_back(static_cast<unsigned char>((value>>16) & 0xFF));
    fullData.push_back(static_cast<unsigned char>((value>>24) & 0xFF));
}

void push32bits(std::vector<unsigned char>& fullData, float value) {
    push32bits(fullData,*reinterpret_cast<int*>(&value));
}

void pushMetaData(std::vector<unsigned char>& fullData, const char (&eightCC)[9],float value) {
    for (int i=0; i<8; i++) {
        fullData.push_back(eightCC[i]);
    }
    push32bits(fullData,*(int*)&value);
}
}


//std::string ipv4_int_to_string(uint32_t in, bool *const success)
//{
//    std::string ret(INET_ADDRSTRLEN, '\0');
//    in = htonl(in);
//    const bool _success = (NULL != inet_ntop(AF_INET, &in, &ret[0], ret.size()));
//    if (success)
//    {
//        *success = _success;
//    }
//    if (_success)
//    {
//        ret.pop_back(); // remove null-terminator required by inet_ntop
//    }
//    else if (!success)
//    {
//        char buf[200] = {0};
//        strerror_r(errno, buf, sizeof(buf));
//        throw std::runtime_error(std::string("error converting ipv4 int to string ") + std::to_string(errno) + std::string(": ") + std::string(buf));
//    }
//    return ret;
//}
//// return is native-endian
//// when an error occurs: if success ptr is given, it's set to false, otherwise a std::runtime_error is thrown.
//uint32_t ipv4_string_to_int(const std::string &in, bool *const success)
//{
//    uint32_t ret;
//    const bool _success = (1 == inet_pton(AF_INET, in.c_str(), &ret));
//    ret = ntohl(ret);
//    if (success)
//    {
//        *success = _success;
//    }
//    else if (!_success)
//    {
//        char buf[200] = {0};
//        strerror_r(errno, buf, sizeof(buf));
//        throw std::runtime_error(std::string("error converting ipv4 string to int ") + std::to_string(errno) + std::string(": ") + std::string(buf));
//    }
//    return ret;
//}

int PONKSender::draw(const std::vector<std::vector<PonkSenderPoint>>& lines, int intensity) {
    vector_float2 origin = 0;
    float normalised_scale = 2.0f;

    std::vector<unsigned char> fullData;
    fullData.reserve(65536);

    int points = 0;
    float j = 0.0f;
    for (const auto& line : lines) {
        j += 1.0f;

        fullData.push_back(PONK_DATA_FORMAT_XY_F32_RGB_U8);
        fullData.push_back(2); //at any time?
        pushMetaData(fullData, "PATHNUMB", j);
        pushMetaData(fullData, "MAXSPEED", 1.0f); //can this go BEFORE pathnumb?
        push16bits(fullData, line.size());

        for (const PonkSenderPoint& point : line) {
            vector_float2 networkPoint = (point.Point + origin) * normalised_scale;
            vector_float4 color = point.Color;
            vector_int4 networkColor = simd_max(vector_int4(0), simd_min(vector_int4(0xFF), vector_int4(color * 0x100)));
            push32bits(fullData, networkPoint.x);
            // Push Y - LSB first
            push32bits(fullData, networkPoint.y);
            // Push R - LSB first
            push8bits(fullData, networkColor.x);
            // Push G - LSB first
            push8bits(fullData, networkColor.y);
            // Push B - LSB first
            push8bits(fullData, networkColor.b);
            points++;
        }
    }

    // Compute necessary chunk count
    size_t chunksCount64 = 1 + fullData.size() / (PONK_MAX_DATA_BYTES_PER_PACKET-sizeof(GeomUdpHeader));
    if (chunksCount64 > 255) {
//        ofLog()<<"Error! too many points. "<<(((PONK_MAX_DATA_BYTES_PER_PACKET*255)-sizeof(GeomUdpHeader))/11)<<" maximum";
    }

    //TODO: truncate point data at 255 chunks

    // Compute data CRC
    unsigned int dataCrc = 0;
    for (auto v: fullData) {
        dataCrc += v;
    }

    // Send all chunks to the desired IP address
    bool isFirst = true;
    size_t written = 0;
    unsigned char chunkNumber = 0;
    unsigned char chunksCount = static_cast<unsigned char>(chunksCount64);
    while (written < fullData.size() || isFirst) {
        isFirst = false;
        // Write packet header - 8 bytes
        GeomUdpHeader header;
        strncpy(header.headerString,PONK_HEADER_STRING,sizeof(header.headerString));
        header.protocolVersion = 0;
        header.senderIdentifier = 123123; // Unique ID (so when changing name in sender, the receiver can just rename existing stream)
        strncpy(header.senderName,"Sample Sender",sizeof(header.senderName));
        header.frameNumber = 0; //WHAT IS THIS FOR
        header.chunkCount = chunksCount;
        header.chunkNumber = chunkNumber;
        header.dataCrc = dataCrc;

        // Prepare buffer
        std::vector<unsigned char> packet;
        size_t dataBytesForThisChunk = std::min<size_t>(fullData.size()-written, PONK_MAX_DATA_BYTES_PER_PACKET);
        packet.resize(sizeof(GeomUdpHeader) + dataBytesForThisChunk);
        // Write header
        memcpy(&packet[0],&header,sizeof(GeomUdpHeader));
        // Write data
        memcpy(&packet[sizeof(GeomUdpHeader)],&fullData[written],dataBytesForThisChunk);
        written += dataBytesForThisChunk;

        socket->sendTo(*dest.get(), &packet.front(), static_cast<unsigned int>(packet.size()));

        chunkNumber++;
    }

    //ofLog()<<"wrote "<<chunkNumber<<" chunks, total bytes "<<written;

    return points;
}
















