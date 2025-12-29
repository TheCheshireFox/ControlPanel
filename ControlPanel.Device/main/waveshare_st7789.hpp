#pragma once

#include <cstdint>
#include <type_traits>
#include <mutex>

#include "driver/spi_master.h"
#include "driver/gpio.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_err.h"
#include "esp_check.h"
#include "esp_log.h"

#include "lvgl.h"

enum class orientation_t {
    portrait,
    landscape,
};

class waveshare_st7789_t
{
    static constexpr const char *TAG = "ST7789";

public:
    waveshare_st7789_t(spi_host_device_t spi_host, gpio_num_t cs, gpio_num_t dc, gpio_num_t rst, gpio_num_t bl,
            uint32_t width = 240, uint32_t height = 320, int spi_clock_hz = 40000000, orientation_t orientation = orientation_t::portrait)
        : _spi_host(spi_host),
          _spi_dev(nullptr),
          _cs(cs),
          _dc(dc),
          _rst(rst),
          _bl(bl),
          _width(width),
          _height(height),
          _spi_clock_hz(spi_clock_hz),
          _orientation(orientation)
    {
    }

    inline esp_err_t init()
    {
        std::unique_lock lock{_sync};

        ESP_RETURN_ON_ERROR(config_gpio(), TAG, "%s", "config_gpio failed");
        ESP_RETURN_ON_ERROR(config_spi_device(), TAG, "%s", "config_spi_device failed");

        init_waveshare_sequence();

        return ESP_OK;
    }

    inline void backlight(bool enable)
    {
        std::unique_lock lock{_sync};

        if (_bl >= 0) {
            gpio_set_level((gpio_num_t)_bl, enable);
        }
    }

    void draw(uint16_t x0, uint16_t y0, uint16_t x1, uint16_t y1, const uint8_t* buffer, uint32_t size)
    {
        std::unique_lock lock{_sync};

        set_window(x0, y0, x1, y1);

        set_dc(true);

        spi_transaction_t t = {};
        t.length    = size * 8;
        t.tx_buffer = buffer;

        esp_err_t err = spi_device_transmit(_spi_dev, &t);
        if (err != ESP_OK) {
            ESP_LOGE(TAG, "flush transmit failed: %s", esp_err_to_name(err));
        }
    }

    inline uint32_t width()  const { return _width;  }
    inline uint32_t height() const { return _height; }
    inline orientation_t orientation() const { return _orientation; }

private:
    inline void delay_ms(uint32_t ms)
    {
        vTaskDelay(pdMS_TO_TICKS(ms));
    }

    inline esp_err_t config_gpio()
    {
        gpio_config_t io_conf = {};
        io_conf.intr_type = GPIO_INTR_DISABLE;
        io_conf.mode = GPIO_MODE_OUTPUT;
        io_conf.pull_down_en = GPIO_PULLDOWN_DISABLE;
        io_conf.pull_up_en   = GPIO_PULLUP_DISABLE;

        uint64_t mask = 0;
        if (_dc  >= 0) mask |= 1ULL << _dc;
        if (_rst >= 0) mask |= 1ULL << _rst;
        if (_bl  >= 0) mask |= 1ULL << _bl;

        io_conf.pin_bit_mask = mask;

        esp_err_t err = gpio_config(&io_conf);
        if (err != ESP_OK) return err;

        set_dc(false);
        backlight(true);

        return ESP_OK;
    }

    inline esp_err_t config_spi_device()
    {
        spi_device_interface_config_t devcfg = {};
        devcfg.mode = 0;
        devcfg.clock_speed_hz = _spi_clock_hz;
        devcfg.spics_io_num = _cs;
        devcfg.queue_size = 4;
        devcfg.flags = 0;

        return spi_bus_add_device(_spi_host, &devcfg, &_spi_dev);
    }

    inline void reset_panel()
    {
        if (_rst < 0) {
            return;
        }

        delay_ms(20);

        gpio_set_level((gpio_num_t)_rst, 0);
        delay_ms(20);
        gpio_set_level((gpio_num_t)_rst, 1);
        delay_ms(20);
    }

    inline void write_cmd(uint8_t cmd)
    {
        set_dc(false);

        spi_transaction_t t = {};
        t.length   = 8;
        t.tx_buffer = &cmd;
        spi_device_transmit(_spi_dev, &t);
    }

    inline void write_data(const uint8_t *data, size_t len)
    {
        set_dc(true);

        spi_transaction_t t = {};
        t.length   = len * 8;
        t.tx_buffer = data;
        spi_device_transmit(_spi_dev, &t);
    }

    template<typename... Ts>
    inline void write_data_bytes(Ts... values)
    {
        static_assert(sizeof...(Ts) > 0, "write_data_bytes requires at least one byte");

        static_assert((std::conjunction_v<std::bool_constant<std::is_integral_v<Ts> || std::is_enum_v<Ts>>...>),
            "write_data_bytes only accepts integral or enum types");

        uint8_t buf[sizeof...(Ts)] = { static_cast<uint8_t>(values)... };

        write_data(buf, sizeof...(Ts));
    }

    inline void set_window(uint16_t x0, uint16_t y0,
                           uint16_t x1, uint16_t y1)
    {
        write_cmd(0x2A);
        write_data_bytes(x0 >> 8, x0 & 0xFF, x1 >> 8, x1 & 0xFF);

        write_cmd(0x2B);
        write_data_bytes(y0 >> 8, y0 & 0xFF, y1 >> 8, y1 & 0xFF);

        write_cmd(0x2C);
    }

    inline void init_waveshare_sequence()
    {
        reset_panel();

        // MADCTL
        write_cmd(0x36);
        if (_orientation == orientation_t::portrait)
        {
            write_data_bytes(0x00);
        } else
        {
            write_data_bytes(0x70);
        }

        // COLMOD = 16-bit
        write_cmd(0x3A);
        write_data_bytes(0x05);

        // PORCH setting
        write_cmd(0xB2);
        write_data_bytes(0x0B, 0x0B, 0x00, 0x33, 0x35);

        // Gate control
        write_cmd(0xB7);
        write_data_bytes(0x11);

        write_cmd(0xBB);
        write_data_bytes(0x35);

        write_cmd(0xC0);
        write_data_bytes(0x2C);

        write_cmd(0xC2);
        write_data_bytes(0x01);

        write_cmd(0xC3);
        write_data_bytes(0x0D);

        write_cmd(0xC4);
        write_data_bytes(0x20);

        write_cmd(0xC6);
        write_data_bytes(0x13);

        write_cmd(0xD0);
        write_data_bytes(0xA4, 0xA1);

        write_cmd(0xD6);
        write_data_bytes(0xA1);

        // Positive voltage gamma
        write_cmd(0xE0);
        write_data_bytes(0xF0, 0x06, 0x0B, 0x0A, 0x09, 0x26, 0x29, 0x33, 0x41, 0x18, 0x16, 0x15, 0x29, 0x2D);

        // Negative voltage gamma
        write_cmd(0xE1);
        write_data_bytes(0xF0, 0x04, 0x08, 0x08, 0x07, 0x03, 0x28, 0x32, 0x40, 0x3B, 0x19, 0x18, 0x2A, 0x2E );

        // Display inversion ON
        write_cmd(0x21);

        // Sleep out + display on
        write_cmd(0x11);
        delay_ms(120);
        write_cmd(0x29);
    }

    inline void set_dc(bool data)
    {
        if (_dc >= 0)
        {
            gpio_set_level((gpio_num_t)_dc, data ? 1 : 0);
        }
    }

private:
    spi_host_device_t    _spi_host;
    spi_device_handle_t  _spi_dev;
    const int            _cs;
    const int            _dc;
    const int            _rst;
    const int            _bl;
    const uint32_t       _width;
    const uint32_t       _height;
    const int            _spi_clock_hz;
    const orientation_t  _orientation;
    std::recursive_mutex _sync;
};