#pragma once

#include <functional>
#include <mutex>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/i2c.h"
#include "esp_timer.h"

#define CALLBACK(that, fn) +[](void* a) { ((decltype(that))a)->fn(); }

typedef struct {
    bool touched;
    uint32_t last_touch_ms;
    uint16_t x;
    uint16_t y;
} touch_point_t;

class cst328_driver_t {
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

        _last_point.last_touch_ms = 0;
        _last_point.touched = false;
        _last_point.x = 1;
        _last_point.y = 1;

        _last_point_mutex = xSemaphoreCreateMutex();
        configASSERT(_last_point_mutex);

        ESP_ERROR_CHECK(i2c_param_config(port, &conf));
        ESP_ERROR_CHECK(i2c_driver_install(port, conf.mode, 0, 0, 0));

        if (_interrupt)
        {
            xTaskCreate(CALLBACK(this, touch_task), "touch_task", 4096, this, 5, &_touch_task_handle);
            configASSERT(_touch_task_handle);
            ESP_ERROR_CHECK(gpio_isr_handler_add((gpio_num_t)interrupt, touch_int_isr, &_touch_task_handle));
        }
    }

    void init()
    {
        uint8_t cfg[2] = {0};
        esp_err_t err = cst328_reg_read(CST328_REG_CONFIG, cfg, sizeof(cfg));

        if (err == ESP_OK) {
            ESP_LOGI("CST328", "Touch OK, CONFIG[0..1]=0x%02X 0x%02X", cfg[0], cfg[1]);
        } else {
            ESP_LOGW("CST328", "Touch controller NOT responding: %s", esp_err_to_name(err));
        }
    }

    touch_point_t get_touch() {
        touch_point_t pt;
        
        if (_interrupt)
        {
            xSemaphoreTake(_last_point_mutex, portMAX_DELAY);
            pt = _last_point;
            xSemaphoreGive(_last_point_mutex);
        }
        else
        {
            ESP_ERROR_CHECK_WITHOUT_ABORT(cst328_read_xy_single(pt));
        }

        return pt;
    }

    void on_touch(std::function<void(const touch_point_t&)> cb) {
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

        for (;;)
        {
            // Block until ISR gives a notification
            ulTaskNotifyTake(pdTRUE, portMAX_DELAY);

            if (cst328_read_xy_single(pt) != ESP_OK) {
                continue;
            }

            xSemaphoreTake(_last_point_mutex, portMAX_DELAY);
            _last_point = pt;
            xSemaphoreGive(_last_point_mutex);

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

        esp_err_t err = i2c_master_cmd_begin(
            _port, cmd, 1000 / portTICK_PERIOD_MS);

        i2c_cmd_link_delete(cmd);
        return err;
    }

    esp_err_t cst328_read_xy_single(touch_point_t& pt)
    {
        uint8_t num_byte = 0;
        uint8_t clear = 0;
        esp_err_t err;

        // 1) read number of touch points (low 4 bits)
        err = cst328_reg_read(CST328_REG_NUM, &num_byte, 1);
        if (err != ESP_OK) {
            return err;
        }
        (void)cst328_reg_write(CST328_REG_NUM, &clear, 1);

        //auto touch_cnt = num_byte & 0x0F;
        
        // 2) read first point coordinates: 3 bytes at XY_REG+1
        uint8_t buf[3] = {0};
        err = cst328_reg_read(CST328_REG_XY + 1, buf, sizeof(buf));
        if (err != ESP_OK) {
            return err;
        }

        // 3) clear status (required, otherwise INT may stay active)
        (void)cst328_reg_write(CST328_REG_NUM, &clear, 1);

        // 4) decode 12-bit X/Y from 3 bytes
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
    SemaphoreHandle_t _last_point_mutex;
    touch_point_t _last_point;
    std::function<void(const touch_point_t&)> _on_touch;
    bool _interrupt;
};