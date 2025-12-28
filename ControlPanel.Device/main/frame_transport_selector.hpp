#pragma once

#include "protocol/transport/uart_transport.hpp"
#include "protocol/transport/bt_uart_transport.hpp"

enum class ft_t
{
    none,
    uart,
    bt_uart
};

template<ft_t type>
struct ft_select;

template<>
struct ft_select<ft_t::uart> { using type = transport::uart_transport_t; };

template<>
struct ft_select<ft_t::bt_uart> { using type = transport::bt_uart_transport_t; };