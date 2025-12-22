#define LV_ASSERT_HANDLER configASSERT(false)

#include <stdio.h>
#include <optional>
#include <mutex>
#include <string.h>

#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/i2c.h"
#include "driver/uart.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "sdkconfig.h"

#include "lvgl.h"

#include "cst328_driver.hpp"
#include "waveshare_st7789.hpp"
#include "volume_display.hpp"
#include "backlight_timer.hpp"
#include "uart.hpp"
#include "uart_log_proto_forwarder.hpp"
#include "lv_sync.hpp"
#include "lvgl_logging.hpp"
#include "protocol.hpp"

static constexpr char TAG[] = "main";

// ST7789T3
#define LCD_SPI_HOST   SPI3_HOST
#define LCD_SPI_CLOCK  60 * 1000000
#define PIN_LCD_MOSI   23
#define PIN_LCD_SCLK   18
#define PIN_LCD_CS     5
#define PIN_LCD_DC     27
#define PIN_LCD_RST    26 // shared with touch RST
#define PIN_LCD_BL     25
#define LCD_WIDTH      240
#define LCD_HEIGHT     320

// CST328
#define I2C_TOUCH_PORT    I2C_NUM_0
#define I2C_TOUCH_FREQ_HZ 400000
#define PIN_TOUCH_SDA  21
#define PIN_TOUCH_SCL  22
#define PIN_TOUCH_RST  26 // shared with lcd RST
#define PIN_TOUCH_INT  4

// SD
#define SD_SPI_HOST SPI2_HOST
#define SD_SCK      14
#define SD_MISO     13
#define SD_MOSI     32
#define SD_CS       33

#define UART_PORT       UART_NUM_0
#define UART_TX         gpio_num_t(1) // 17
#define UART_RX         gpio_num_t(3) // 16
#define UART_BUF_SIZE   8096
#define UART_BAUDRATE   921600

#define BL_TIMER_LONG  uint64_t(3600 * 1000)
#define BL_TIMER_SHORT uint64_t(30 * 1000)

static std::optional<cst328_driver_t> cst328_driver;
static std::optional<waveshare_st7789_t> st7789_driver;
static std::optional<volume_display_t> volume_display;
static std::optional<backlight_timer_t<waveshare_st7789_t>> backlight_timer;
static std::optional<uart_t> uart;

static void spi_bus_init(void)
{
    spi_bus_config_t buscfg = {
        .mosi_io_num = PIN_LCD_MOSI,
        .miso_io_num = -1,
        .sclk_io_num = PIN_LCD_SCLK,
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
        .max_transfer_sz = LCD_WIDTH * LCD_HEIGHT * 2 + 8,
    };
    ESP_ERROR_CHECK(spi_bus_initialize(SPI3_HOST, &buscfg, SPI_DMA_CH_AUTO));
}

static void touch_read_cb(lv_indev_t *indev, lv_indev_data_t *data)
{
    const uint32_t TOUCH_TIMEOUT_MS = 40;

    (void)indev;

    if (!cst328_driver) {
        data->state = LV_INDEV_STATE_RELEASED;
        return;
    }

    auto pt = cst328_driver->get_touch();
    auto touched = ((esp_timer_get_time() / 1000) - pt.last_touch_ms) < TOUCH_TIMEOUT_MS;

    data->state = touched ? LV_INDEV_STATE_PRESSED : LV_INDEV_STATE_RELEASED;

    switch (st7789_driver->orientation())
    {
        case waveshare_st7789_t::orientation_t::portrait:
            data->point.x = pt.x;
            data->point.y = pt.y;
            break;
        case waveshare_st7789_t::orientation_t::landscape:
            data->point.x = pt.y;
            data->point.y = st7789_driver->height() - pt.x;
            break;
    }

    data->point.x = LV_CLAMP(0, data->point.x, (int32_t)st7789_driver->width());
    data->point.y = LV_CLAMP(0, data->point.y, (int32_t)st7789_driver->height());
}

void touch_init_for_lvgl(void)
{
    static auto touch_indev = lv_indev_create();
    lv_indev_set_type(touch_indev, LV_INDEV_TYPE_POINTER);
    lv_indev_set_read_cb(touch_indev, touch_read_cb);

    ESP_LOGI(TAG, "LVGL touch initialized");
}

void panel_init(void)
{
    ESP_ERROR_CHECK(gpio_install_isr_service(0));

    spi_bus_init();

    cst328_driver.emplace(I2C_TOUCH_PORT, I2C_TOUCH_FREQ_HZ, PIN_TOUCH_SDA, PIN_TOUCH_SCL, PIN_TOUCH_INT);
    st7789_driver.emplace(SPI3_HOST, PIN_LCD_CS, PIN_LCD_DC, PIN_LCD_RST, PIN_LCD_BL, LCD_HEIGHT, LCD_WIDTH, LCD_SPI_CLOCK, waveshare_st7789_t::orientation_t::landscape);
    backlight_timer.emplace(*st7789_driver, BL_TIMER_SHORT);
    
    cst328_driver->on_touch(+[](const touch_point_t& pt) { backlight_timer->kick(); });
    cst328_driver->init();
    backlight_timer->init();
    st7789_driver->init();
    
    st7789_driver->backlight(true);

    ESP_LOGI(TAG, "Panel (LCD + touch + BL) initialized");
}

static void lvgl_init_core()
{
    esp_timer_init();
    lv_init();
    lv_tick_set_cb(+[](){ return (uint32_t)(esp_timer_get_time() / 1000); });
    lvgl_init_logging();

    ESP_LOGI(TAG, "LVGL core initialized");
}

static void lvgl_timer_init()
{
    xTaskCreatePinnedToCore(+[](void*)
    {
        while(1) {
            TickType_t next;
            {
                std::scoped_lock lock{lv_timer_sync};
                next = pdMS_TO_TICKS(lv_timer_handler());
            }
            vTaskDelay(next > 0 ? next : 1);
        }
    }, "lv_timer_handler", 16384, nullptr, 5, nullptr, tskNO_AFFINITY);

    ESP_LOGI(TAG, "LVGL timer started");
}

void uart_init(void)
{
    uart.emplace(UART_PORT, UART_TX, UART_RX, UART_BUF_SIZE, UART_BAUDRATE);
    uart->init();

    ESP_LOGI(TAG, "UART initialized");
}

void uart_register_handler()
{
    uart->register_data_handler(+[](std::span<const uint8_t> data)
    {
        backlight_timer->kick();

        auto bmsg = parse_bridge_message(data);
        if (streams_message_t* msg = std::get_if<streams_message_t>(&bmsg))
        {
            ESP_LOGD(TAG, "refresh updated=%d deleted=%d", msg->updated.size(), msg->deleted.size());

            volume_display->refresh(msg->updated, msg->deleted);
            auto ms = volume_display->size() > 0
                ? BL_TIMER_LONG
                : BL_TIMER_SHORT;
            backlight_timer->set_timeout(ms);
        }
        else if (icon_message_t* msg = std::get_if<icon_message_t>(&bmsg))
        {
            ESP_LOGD(TAG, "icon source=%s agent_id=%s sz=%d", msg->source.c_str(), msg->agent_id.c_str(), msg->icon.size());
            volume_display->update_icon(msg->source, msg->agent_id, msg->size, msg->size, msg->icon);
        }
    });
}

static lv_display_t* st7789_create_lvgl_display()
{
    assert(st7789_driver);

    const auto hor_res = st7789_driver->width();
    const auto ver_res = st7789_driver->height();

    auto disp = lv_display_create(hor_res, ver_res);
    lv_display_set_color_format(disp, LV_COLOR_FORMAT_RGB565);

    st7789_driver->register_flush_cb(disp);

    const size_t buf_bytes = hor_res * ver_res / 5 * LV_COLOR_FORMAT_GET_SIZE(LV_COLOR_FORMAT_RGB565);
    auto buf = static_cast<lv_color_t*>(heap_caps_malloc(buf_bytes, MALLOC_CAP_DMA));
    configASSERT(buf);

    lv_display_set_buffers(disp, buf, nullptr, buf_bytes, LV_DISPLAY_RENDER_MODE_PARTIAL);

    ESP_LOGI(TAG, "LVGL display created");

    return disp;
}

extern "C" void app_main(void)
{
    uart_init();
    uart_log_proto_forwarder::init(&uart.value());

    ESP_LOGI(TAG, "Starting app_main...");

    panel_init();
    lvgl_init_core();
    auto disp = st7789_create_lvgl_display();
    touch_init_for_lvgl();
    app_style::init(disp);
    lvgl_timer_init();

    volume_display.emplace(0, 0, LV_PCT(100), LV_PCT(100));
    volume_display->on_volume_change(+[](const event_id& id, float volume)
    {
        uart->send_data(serialize_bridge_message(set_volume_message_t {
            .id = { id.id, id.agent_id },
            .volume = volume
        }));
    });
    volume_display->on_mute_change(+[](const event_id& id, bool mute)
    {
        uart->send_data(serialize_bridge_message(set_mute_message_t {
            .id = { id.id, id.agent_id },
            .mute = mute
        }));
    });
    volume_display->on_icon_missing(+[](const std::string& source, const std::string& agent_id)
    {
        uart->send_data(serialize_bridge_message(get_icon_message_t {
            .source = source,
            .agent_id = agent_id
        }));
    });

    uart_register_handler();
    uart->send_data(serialize_bridge_message(request_refresh_message_t{}), 1000, std::numeric_limits<uint32_t>::max());

    ESP_LOGI(TAG, "Initialization completed");
}