#pragma once

#include <mutex>
#include <array>

#include "freertos/FreeRTOS.h"

struct ack_waiter_t
{
    bool wait(uint16_t seq, uint32_t timeout_ms)
    {
        slot_t* slot = nullptr;
        {
            std::unique_lock lock{_sync};
            for (auto& s: _slots)
            {
                if (s.free)
                {
                    s.free = false;
                    s.seq = seq;
                    s.waiter = xTaskGetCurrentTaskHandle();
                    slot = &s;
                    break;
                }
            }
        }

        if (slot == nullptr)
            return false;
        
        uint32_t ack_seq = 0;
        if (xTaskNotifyWait(0, 0xFFFFFFFF, &ack_seq, pdMS_TO_TICKS(timeout_ms)) && (uint16_t)ack_seq == seq)
        {
            return true;
        }

        {
            std::unique_lock lock{_sync};
            slot->reset();
        }

        return false;
    }

    void notify(uint16_t seq)
    {
        TaskHandle_t waiter = nullptr;
        {
            std::unique_lock lock{_sync};

            for (auto& s: _slots)
            {
                if (!s.free && s.seq == seq)
                {
                    waiter = s.reset();
                    break;
                }
            }
        }

        if (waiter)
            xTaskNotify(waiter, (uint32_t)seq, eSetValueWithOverwrite);
    }

private:
    struct slot_t
    {
        bool free = false;
        uint16_t seq = 0;
        TaskHandle_t waiter = nullptr;

        TaskHandle_t reset()
        {
            auto w = waiter;

            free = false;
            seq = 0;
            waiter = nullptr;

            return w;
        }
    };

    std::recursive_mutex _sync;
    std::array<slot_t, 16> _slots = {};
};