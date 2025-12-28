#pragma once

#include <string>
#include <condition_variable>

#include "esp_log.h"
#include "esp_check.h"
#include "esp_bt.h"
#include "esp_bt_main.h"
#include "esp_gap_bt_api.h"
#include "esp_bt_device.h"
#include "esp_spp_api.h"

namespace transport
{
    struct bt_uart_transport_t
    {
        static constexpr char TAG[] = "BT UART";

        bt_uart_transport_t(const std::string& server_name, const std::string& dev_name)
            : _server_name(server_name), _dev_name(dev_name)
        {
        }

        void init()
        {
            configASSERT(!instance);

            instance = this;

            ESP_ERROR_CHECK(esp_bt_controller_mem_release(ESP_BT_MODE_BLE));
            esp_bt_controller_config_t bt_cfg = BT_CONTROLLER_INIT_CONFIG_DEFAULT();

            ESP_ERROR_CHECK(esp_bt_controller_init(&bt_cfg));
            ESP_ERROR_CHECK(esp_bt_controller_enable(ESP_BT_MODE_CLASSIC_BT));

            esp_bluedroid_config_t bluedroid_cfg = BT_BLUEDROID_INIT_CONFIG_DEFAULT();

            ESP_ERROR_CHECK(esp_bluedroid_init_with_cfg(&bluedroid_cfg));
            ESP_ERROR_CHECK(esp_bluedroid_enable());
            ESP_ERROR_CHECK(esp_bt_gap_register_callback(
                +[](esp_bt_gap_cb_event_t e, esp_bt_gap_cb_param_t* p) { instance->esp_bt_gap_cb(e, p); }));
            ESP_ERROR_CHECK(esp_spp_register_callback(
                +[](esp_spp_cb_event_t e, esp_spp_cb_param_t* p) { instance->esp_spp_cb(e, p); }));

            esp_spp_cfg_t bt_spp_cfg {
                .mode = ESP_SPP_MODE_CB,
                .enable_l2cap_ertm = true,
                .tx_buffer_size = 0,
            };

            ESP_ERROR_CHECK(esp_spp_enhanced_init(&bt_spp_cfg));

            esp_bt_io_cap_t cap = ESP_BT_IO_CAP_NONE;
            esp_bt_gap_set_security_param(esp_bt_sp_param_t::ESP_BT_SP_IOCAP_MODE, &cap, sizeof(cap));
        }

        void write(std::span<uint8_t> data)
        {
            std::unique_lock lock{_write_sync};

            ESP_LOGI(TAG, "preparing to write sz=%d cong=%d handle=%d", data.size(), (int)_cong, _handle);

            _cong_cv.wait(lock, +[](){ return !_cong && _handle; });

            ESP_ERROR_CHECK_WITHOUT_ABORT(esp_spp_write(_handle, data.size(), data.data()));
        }

        template<typename F>
        void on_receive(F&& f)
        {
            _on_recieve = std::move(f);
        }

    private:
        void esp_spp_cb(esp_spp_cb_event_t event, esp_spp_cb_param_t *param)
        {
            switch (event)
            {
                case ESP_SPP_INIT_EVT:
                    ESP_LOGI(TAG, "%s", "init");
                    esp_spp_start_srv(ESP_SPP_SEC_AUTHENTICATE, ESP_SPP_ROLE_SLAVE, 1, _server_name.c_str());
                    break;
                case ESP_SPP_CLOSE_EVT:
                {
                    ESP_LOGI(TAG, "ESP_SPP_CLOSE_EVT status:%d handle:%" PRIu32 " close_by_remote:%d", param->close.status, param->close.handle, param->close.async);

                    std::unique_lock lock{_write_sync};
                    _handle = 0;
                    _cong_cv.notify_all();
                    break;
                }
                case ESP_SPP_START_EVT:
                    ESP_LOGI(TAG, "%s", "start");
                    esp_bt_gap_set_device_name(_dev_name.c_str());
                    esp_bt_gap_set_scan_mode(ESP_BT_CONNECTABLE, ESP_BT_GENERAL_DISCOVERABLE);
                    break;
                case ESP_SPP_DATA_IND_EVT:
                    if (_on_recieve) _on_recieve(std::span<uint8_t>(param->data_ind.data, param->data_ind.len));
                    break;
                case ESP_SPP_WRITE_EVT:
                {
                    if (param->write.cong) ESP_LOGI(TAG, "congested status: %d", param->write.cong);
                    
                    std::unique_lock lock{_write_sync};
                    _cong = param->write.cong;
                    _cong_cv.notify_all();
                    break;
                }
                case ESP_SPP_SRV_OPEN_EVT:                    
                {
                    ESP_LOGI(TAG, "ESP_SPP_SRV_OPEN_EVT status:%d handle:%"PRIu32", rem_bda:[%s]", param->srv_open.status, param->srv_open.handle, bda2str(param->srv_open.rem_bda));

                    std::unique_lock lock{_write_sync};
                    _handle = param->srv_open.handle;
                    _cong_cv.notify_all();
                    break;
                }
                case ESP_SPP_CONG_EVT:
                {
                    ESP_LOGI(TAG, "congested status: %d", param->cong.cong);
                    
                    std::unique_lock lock{_write_sync};
                    _cong = param->cong.cong;
                    _cong_cv.notify_all();
                    break;
                }
                default:
                    ESP_LOGI(TAG, "spp event: %d", event);
                    break;
            }
        }

        void esp_bt_gap_cb(esp_bt_gap_cb_event_t event, esp_bt_gap_cb_param_t *param)
        {
            switch (event)
            {
                case ESP_BT_GAP_CFM_REQ_EVT:
                    ESP_LOGI(TAG, "SSP CFM_REQ, num=%" PRIu32, param->cfm_req.num_val);
                    ESP_ERROR_CHECK(esp_bt_gap_ssp_confirm_reply(param->cfm_req.bda, true));
                    break;
                case ESP_BT_GAP_AUTH_CMPL_EVT:
                    if (param->auth_cmpl.stat == ESP_BT_STATUS_SUCCESS)
                    {
                        ESP_LOGI(TAG, "authentication success: %s bda:[%s]", param->auth_cmpl.device_name, bda2str(param->auth_cmpl.bda));
                    }
                    else
                    {
                        ESP_LOGE(TAG, "authentication failed, status:%d", param->auth_cmpl.stat);
                    }
                    break;
                default:
                    ESP_LOGI(TAG, "gap event: %d", event);
                    break;
            }
            return;
        }

        static const char* bda2str(uint8_t* bda)
        {
            static char str[18];

            if (bda == nullptr)
                return nullptr;

            sprintf(str, "%02x:%02x:%02x:%02x:%02x:%02x", bda[0], bda[1], bda[2], bda[3], bda[4], bda[5]);
            return str;
        }

    private:
        inline static bt_uart_transport_t* instance = nullptr;
        inline static uint32_t _handle = 0;
        inline static std::mutex _write_sync;
        inline static std::condition_variable _cong_cv{};
        inline static bool _cong = false;

        const std::string _server_name;
        const std::string _dev_name;
        std::function<void(std::span<uint8_t>)> _on_recieve{};
    };
};