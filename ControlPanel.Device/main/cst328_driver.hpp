#pragma once

#include <functional>
#include <mutex>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/i2c.h"
#include "esp_timer.h"

#include "esp_utility.hpp"

struct semaphore_t
{
    semaphore_t() : _s(xSemaphoreCreateRecursiveMutex()) { }
    semaphore_t(semaphore_t&& other) : _s(other._s) { other._s = nullptr; }
    semaphore_t(semaphore_t&) = delete;
    semaphore_t(const semaphore_t&) = delete;

    void lock() { if (_s != nullptr) xSemaphoreTakeRecursive(_s, portMAX_DELAY); }
    void unlock() { if (_s != nullptr) xSemaphoreGiveRecursive(_s); }

    ~semaphore_t() { if (_s != nullptr) vSemaphoreDelete(_s); }
private:
    SemaphoreHandle_t _s = nullptr;
};

typedef struct {
    bool touched = false;
    uint32_t last_touch_ms = 0;
    uint16_t x = 1;
    uint16_t y = 1;
} touch_point_t;

class cst328_driver_t {
    static constexpr char TAG[] = "CST328";
    static constexpr uint8_t CST328_I2C_ADDR = 0x1A;
    static constexpr uint16_t CST328_REG_NUM = 0xD005;
    static constexpr uint16_t CST328_REG_XY = 0xD000;
    static constexpr uint16_t CST328_REG_CONFIG = 0x8047;

public:
    cst328_driver_t(i2c_port_t port, uint32_t clock, int sda, int scl, int interrupt = -1)
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
        
        i2c_config_t conf = {
            .mode = I2C_MODE_MASTER,
            .sda_io_num    = sda,
            .scl_io_num    = scl,
            .sda_pullup_en = GPIO_PULLUP_ENABLE,
            .scl_pullup_en = GPIO_PULLUP_ENABLE,
            .master = {
                .clk_speed = clock
            }
        };

        ESP_ERROR_CHECK(i2c_param_config(port, &conf));
        ESP_ERROR_CHECK(i2c_driver_install(port, conf.mode, 0, 0, 0));

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

            if (cst328_read_xy_single(pt) != ESP_OK) {
                continue;
            }

            {
                std::unique_lock lock{_sync};
                _last_point = pt;
            }

            if (_on_touch) {
                _on_touch(pt);
            }
        }
    }

    esp_err_t cst328_reg_read(uint16_t reg, uint8_t *data, size_t len)
    {
        uint8_t reg_bytes[2] = {
            (uint8_t)(reg >> 8),
            (uint8_t)(reg & 0xFF),
        };

        return i2c_master_write_read_device(
            _port,
            CST328_I2C_ADDR,
            reg_bytes, sizeof(reg_bytes),
            data, len,
            1000 / portTICK_PERIOD_MS
        );
    }

    esp_err_t cst328_reg_write(uint16_t reg, const uint8_t *data, size_t len)
    {
        i2c_cmd_handle_t cmd = i2c_cmd_link_create();

        i2c_master_start(cmd);
        i2c_master_write_byte(cmd, (CST328_I2C_ADDR << 1) | I2C_MASTER_WRITE, true);
        i2c_master_write_byte(cmd, (uint8_t)(reg >> 8), true);
        i2c_master_write_byte(cmd, (uint8_t)(reg & 0xFF), true);

        if (len && data) {
            i2c_master_write(cmd, data, len, true);
        }

        i2c_master_stop(cmd);

        esp_err_t err = i2c_master_cmd_begin(_port, cmd, pdMS_TO_TICKS(1000));

        i2c_cmd_link_delete(cmd);
        return err;
    }

    esp_err_t cst328_read_xy_single(touch_point_t& pt)
    {
        // read first point coordinates: 3 bytes at XY_REG+1
        uint8_t buf[3] = {0};
        auto err = cst328_reg_read(CST328_REG_XY + 1, buf, sizeof(buf));
        if (err != ESP_OK) {
            return err;
        }

        uint8_t clear = 0;
        (void)cst328_reg_write(CST328_REG_NUM, &clear, 1);

        // decode 12-bit X/Y from 3 bytes
        //    matches:
        //    x = (buf0 << 4) + ((buf2 & 0xF0) >> 4)
        //    y = (buf1 << 4) + ( buf2 & 0x0F)
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
};