#pragma once

#include <vector>
#include <string>
#include <cstdint>

inline const std::string base64_chars =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    "abcdefghijklmnopqrstuvwxyz"
    "0123456789+/";

inline std::vector<uint8_t> base64_decode(const std::string &encoded)
{
    auto in_len = encoded.size();
    auto i = 0;
    auto in_ = 0;
    uint8_t char_array_4[4], char_array_3[3];
    std::vector<uint8_t> ret;

    while (in_len-- && (encoded[in_] != '=' && (isalnum(encoded[in_]) || encoded[in_] == '+' || encoded[in_] == '/')))
    {
        char_array_4[i++] = encoded[in_++];
        if (i == 4)
        {
            for (i = 0; i < 4; i++)
                char_array_4[i] = base64_chars.find(char_array_4[i]);

            char_array_3[0] =  (char_array_4[0] << 2)        + ((char_array_4[1] & 0x30) >> 4);
            char_array_3[1] = ((char_array_4[1] & 0x0F) << 4)+ ((char_array_4[2] & 0x3C) >> 2);
            char_array_3[2] = ((char_array_4[2] & 0x03) << 6)+   char_array_4[3];

            ret.insert(ret.end(), char_array_3, char_array_3 + 3);

            i = 0;
        }
    }

    if (i)
    {
        for (int j = i; j < 4; j++)
            char_array_4[j] = 0;

        for (int j = 0; j < 4; j++)
            char_array_4[j] = base64_chars.find(char_array_4[j]);

        char_array_3[0] =  (char_array_4[0] << 2)        + ((char_array_4[1] & 0x30) >> 4);
        char_array_3[1] = ((char_array_4[1] & 0x0F) << 4)+ ((char_array_4[2] & 0x3C) >> 2);
        char_array_3[2] = ((char_array_4[2] & 0x03) << 6)+   char_array_4[3];

        for (int j = 0; j < i - 1; j++)
            ret.push_back(char_array_3[j]);
    }

    return ret;
}