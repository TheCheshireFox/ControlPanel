#pragma once

#include <span>

#include "freertos/FreeRTOS.h"
#include "esp_log.h"

class buffer_queue_t
{
    static constexpr char TAG[] = "BQ";

public:
    buffer_queue_t(std::size_t block_size, std::size_t count)
    {
        _block_size = block_size;
        _mem = {(uint8_t*)malloc(block_size * count), block_size * count};
        
        configASSERT(_queue = xQueueCreate(count, sizeof(uint8_t*)));
        
        for (auto i = 0; i < count; i++)
        {
            auto p = _mem.data() + i * block_size;
            xQueueSend(_queue, &p, portMAX_DELAY);
        }
    }

    std::size_t block_size() const { return _block_size; }

    std::span<uint8_t> take(TickType_t timeout_ms = portMAX_DELAY)
    {
        uint8_t* ptr;
        if(xQueueReceive(_queue, &ptr, pdMS_TO_TICKS(timeout_ms)))
        {
            return std::span<uint8_t>{ptr, _block_size};
        }
        
        ESP_LOGE(TAG, "%s", "take failed");
        return std::span<uint8_t>{};
    }

    void give(std::span<uint8_t> block)
    {
        if (block.data() < _mem.data() || block.data() > (_mem.data() + _mem.size()) || block.size() != _block_size)
        {
            ESP_LOGE(TAG, "cannot return invalid block ptr=%p sz=%d", block.data(), block.size());
            return;
        }

        auto p = block.data();
        configASSERT(xQueueSend(_queue, &p, portMAX_DELAY));
    }

private:
    std::size_t _block_size;
    std::span<uint8_t> _mem;
    QueueHandle_t _queue;
};