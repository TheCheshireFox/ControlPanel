#pragma once

#include <condition_variable>

#include "framer.hpp"
#include "transport/frame_transport.hpp"

namespace transport
{
    namespace details
    {
        template<class T>
        constexpr bool is_u8_array_v = false;

        template<std::size_t N>
        constexpr bool is_u8_array_v<std::array<uint8_t, N>> = true;
    }

    template<typename TTransport, std::array Magic, std::size_t BufferSize = 16 * 1024, std::size_t MAX_TX_FRAME = 256, std::size_t SEND_QUEUE_SIZE = 8>
    requires frame_transport_t<TTransport> && details::is_u8_array_v<std::remove_cvref_t<decltype(Magic)>>
    class frame_host_connection_t
    {
        using connection_framer_t = framer_t<Magic.size(), BufferSize>;
        
        static constexpr size_t MAX_TX_BODY = MAX_TX_FRAME - connection_framer_t::calc_frame_size(0);

    public:
        static constexpr char TAG[] = "FP";

        frame_host_connection_t(TTransport& transport)
            : _transport(transport)
            , _framer(Magic)
        {
            _transport.on_receive([&](auto d){ on_data(d); });
        }

        void init(void)
        {
            _send_queue = xQueueCreate(SEND_QUEUE_SIZE, sizeof(frame_info_t));
            configASSERT(_send_queue);
            
            xTaskCreate(THIS_CALLBACK(this, send_task), "send_task", 4096, this, 10, &_send_task);
        }

        template<typename F>
        void register_data_handler(F&& cb)
        {
            _data_handler = std::move(cb);
        }

        void send(std::span<uint8_t> data, uint32_t retry_interval_ms = 1000, uint32_t retry_count = 3)
        {
            auto frame_size = _framer.calc_frame_size(data.size());
            if (frame_size > MAX_TX_FRAME || data.size() > MAX_TX_BODY)
            {
                ESP_LOGE(TAG, "data too large sz=%d frame_sz=%d max_data=%d max_frame=%d", data.size(), frame_size, MAX_TX_BODY, MAX_TX_FRAME);
                return;
            }
            
            std::unique_lock lock{_send_sync};

            frame_info_t frame_info {
                .seq = ++_seq_cnt,
                .type = frame_type_t::data,
                .size = data.size(),
                .r_interval = retry_interval_ms,
                .r_count = retry_count
            };
            std::memcpy(frame_info.data, data.data(), data.size());

            do
            {
                if (xQueueSend(_send_queue, &frame_info, pdMS_TO_TICKS(retry_interval_ms)))
                    break;
                
            } while (--frame_info.r_count > 0);
        }

    private:
        void on_data(std::span<uint8_t> data)
        {
            _framer.feed(data, [&](const frame_t& frame)
            {
                switch (frame.type)
                {
                    case frame_type_t::ack:
                        {
                            std::unique_lock lock{_ack_sync};
                            _last_ack = frame.seq;
                            _new_ack.notify_all();
                        }
                        break;
                    case frame_type_t::data:
                        frame_t ack_frame{frame.seq, frame_type_t::ack, {}};
                        send_bytes(to_bytes(_ack_buffer, ack_frame));

                        if (_data_handler) _data_handler(frame.data);
                        
                        break;
                }
            });
        }

        void send_task()
        {
            std::array<uint8_t, MAX_TX_FRAME> buffer;
            frame_info_t frame_info;
            while (true)
            {
                if (!xQueueReceive(_send_queue, &frame_info, portMAX_DELAY))
                    continue;
                
                frame_t frame{frame_info.seq, frame_info.type, std::span<uint8_t>(frame_info.data, frame_info.size)};
                auto frame_bytes = to_bytes(buffer, frame);

                for (int i = 0; i < frame_info.r_count; i++)
                {
                    send_bytes(frame_bytes);

                    std::unique_lock lock{_ack_sync};
                    auto success = _new_ack.wait_for(lock, std::chrono::milliseconds(frame_info.r_interval), [&]() {
                        return _last_ack == frame.seq;
                    });

                    if (success)
                        break;
                }
            }
        }

        std::span<uint8_t> to_bytes(std::span<uint8_t> buffer, const frame_t& frame)
        {
            auto sz = _framer.to_bytes(buffer, frame);
            return buffer.subspan(0, sz);
        }

        void send_bytes(std::span<uint8_t> bytes)
        {
            std::scoped_lock lock{_tx_sync};
            _transport.write(bytes);
        }

    private:
        struct frame_info_t
        {
            uint16_t seq;
            frame_type_t type;
            uint8_t data[MAX_TX_BODY];
            uint32_t size;
            uint32_t r_interval;
            uint32_t r_count;
        };

        TTransport& _transport;
        connection_framer_t _framer;

        std::function<void(std::span<const uint8_t>)> _data_handler;

        TaskHandle_t _send_task;
        QueueHandle_t _send_queue;
        std::mutex _send_sync;
        std::mutex _tx_sync;

        std::array<uint8_t, connection_framer_t::calc_frame_size(0) * 2> _ack_buffer;
        uint16_t _last_ack = 0;
        std::condition_variable _new_ack;
        std::mutex _ack_sync;
        uint16_t _seq_cnt = 0;
    };
}