#pragma once

#include <functional>
#include <mutex>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/i2c_master.h"
#include "esp_timer.h"

#include "esp_utility.hpp"

struct touch_point_t {
    bool touched = false;
    uint32_t last_touch_ms = 0;
    uint16_t x = 1;
    uint16_t y = 1;
};

class cst328_driver_t {
    static constexpr char TAG[] = "CST328";
    static constexpr uint8_t CST328_I2C_ADDR = 0x1A;
    static constexpr uint16_t CST328_REG_NUM = 0xD005;
    static constexpr uint16_t CST328_REG_XY = 0xD000;
    static constexpr uint16_t CST328_REG_CONFIG = 0x8047;

public:
    cst328_driver_t(i2c_port_t port, uint32_t clock, gpio_num_t sda, gpio_num_t scl, gpio_num_t interrupt = gpio_num_t::GPIO_NUM_NC)
        : _port(port), _interrupt(interrupt != -1)
    {
        if (interrupt != -1)
        {
            gpio_config_t int_conf = {};
            int_conf.intr_type    = GPIO_INTR_NEGEDGE;
            int_conf.mode         = GPIO_MODE_INPUT;
            int_conf.pin_bit_mask = (1ULL << interrupt);
            int_conf.pull_down_en = gpio_pulldown_t::GPIO_PULLDOWN_DISABLE;
            int_conf.pull_up_en   = gpio_pullup_t::GPIO_PULLUP_ENABLE;
            ESP_ERROR_CHECK(gpio_config(&int_conf));
        }
        
        i2c_master_bus_config_t conf = {
            .i2c_port = port,
            .sda_io_num = sda,
            .scl_io_num = scl,
            .clk_source = I2C_CLK_SRC_DEFAULT,
            .glitch_ignore_cnt = 7,
            .flags = {
                .enable_internal_pullup = true
            }
        };

        i2c_master_bus_handle_t i2c;
        ESP_ERROR_CHECK(i2c_new_master_bus(&conf, &i2c));

        i2c_device_config_t dev_config {
            .dev_addr_length = I2C_ADDR_BIT_LEN_7,
            .device_address = CST328_I2C_ADDR,
            .scl_speed_hz = clock
        };
        
        ESP_ERROR_CHECK(i2c_master_bus_add_device(i2c, &dev_config, &_dev));

        if (_interrupt)
        {
            xTaskCreate(THIS_CALLBACK(this, touch_task), "touch_task", 4096, this, 5, &_touch_task_handle);
            configASSERT(_touch_task_handle);
            ESP_ERROR_CHECK(gpio_isr_handler_add((gpio_num_t)interrupt, touch_int_isr, &_touch_task_handle));
        }
    }

    void init(bool recalibrate = false)
    {
        uint8_t cfg[2] = {0};
        ESP_ERROR_CHECK(cst328_reg_read(CST328_REG_CONFIG, cfg, sizeof(cfg)));

        if (recalibrate)
        {
            ESP_LOGI(TAG, "Calibrating...");

            uint8_t cmd = 0x04;
            ESP_ERROR_CHECK(cst328_reg_write(0xD104, &cmd, 1));

            vTaskDelay(pdMS_TO_TICKS(250));

            ESP_LOGI(TAG, "Calibrated");
        }

        ESP_LOGI(TAG, "Initialized");
    }

    touch_point_t get_touch() {
        std::unique_lock lock{_sync};

        touch_point_t pt;
        
        if (_interrupt)
        {
            pt = _last_point;
        }
        else
        {
            ESP_ERROR_CHECK_WITHOUT_ABORT(cst328_read_xy_single(pt));
        }

        return pt;
    }

    template<typename F>
    void on_touch(F&& cb) {
        std::unique_lock lock{_sync};

        if (!_interrupt)
            return;

        _on_touch = std::move(cb);
    }

private:
    static void IRAM_ATTR touch_int_isr(void *task_handle)
    {
        BaseType_t xHigherPriorityTaskWoken = pdFALSE;

        vTaskNotifyGiveFromISR(*(TaskHandle_t*)task_handle, &xHigherPriorityTaskWoken);

        if (xHigherPriorityTaskWoken) {
            portYIELD_FROM_ISR();
        }
    }

    void touch_task()
    {
        touch_point_t pt;

        while (true)
        {
            ulTaskNotifyTake(pdTRUE, portMAX_DELAY);

            if (ESP_ERROR_CHECK_WITHOUT_ABORT(cst328_read_xy_single(pt)) != ESP_OK) {
                continue;
            }

            {
                std::unique_lock lock{_sync};
                _last_point = pt;
            }

            if (_on_touch) _on_touch(pt);
        }
    }

    esp_err_t cst328_reg_read(uint16_t reg, uint8_t *data, size_t len)
    {
        uint8_t reg_bytes[2] = {
            (uint8_t)(reg >> 8),
            (uint8_t)(reg & 0xFF),
        };

        return i2c_master_transmit_receive(_dev, reg_bytes, sizeof(reg_bytes), data, len, 1000);
    }

    esp_err_t cst328_reg_write(uint16_t reg, const uint8_t *data, size_t len)
    {
        if (!data || !len)
            return ESP_ERR_INVALID_ARG;

        uint8_t reg_bytes[2] = { (uint8_t)(reg >> 8), (uint8_t)(reg & 0xFF) };
        i2c_master_transmit_multi_buffer_info_t buf[2] = {
            { .write_buffer = reg_bytes, .buffer_size = sizeof(reg_bytes) },
            { .write_buffer = data, .buffer_size = len }
        };

        return i2c_master_multi_buffer_transmit(_dev, buf, std::size(buf), 1000);
    }

    esp_err_t cst328_read_xy_single(touch_point_t& pt)
    {
        uint8_t buf[3] = {0};
        auto err = cst328_reg_read(CST328_REG_XY + 1, buf, sizeof(buf));
        if (err != ESP_OK) {
            return err;
        }

        uint8_t clear = 0;
        (void)cst328_reg_write(CST328_REG_NUM, &clear, 1);

        uint16_t x = ((uint16_t)buf[0] << 4) | ((buf[2] & 0xF0) >> 4);
        uint16_t y = ((uint16_t)buf[1] << 4) |  (buf[2] & 0x0F);

        pt.touched = true;
        pt.last_touch_ms = esp_timer_get_time() / 1000;
        pt.x = x;
        pt.y = y;

        return ESP_OK;
    }

private:
    i2c_port_t _port;
    TaskHandle_t _touch_task_handle;
    std::recursive_mutex _sync;
    touch_point_t _last_point;
    std::function<void(const touch_point_t&)> _on_touch;
    const bool _interrupt;
    i2c_master_dev_handle_t _dev;
};