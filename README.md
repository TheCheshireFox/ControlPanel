## Overview

ControlPanel is a system that aims to simplify one thing: controlling audio across multiple machines using a single physical interface.

It exposes audio controls from multiple machines through a dedicated touchscreen device, removing the need to switch between system mixers and application-level controls.

The system is composed of three parts:

- **Agents** — lightweight services running on Windows or Linux machines that interface with the local audio system.
- **Bridge** — a Linux service that acts as a central hub for agents and communicates with the device over UART or Bluetooth RFCOMM.
- **Device** — an ESP32-based touchscreen dashboard built with LVGL.

## Features

- **Centralized control surface** for audio across multiple machines
- **Physical touchscreen interface** based on ESP32 and LVGL
- **Cross-platform agents** for Windows and Linux
- **Multiple transport options** between bridge and device (UART, Bluetooth RFCOMM)
- **Low-footprint device firmware** with most processing handled off-device

## Supported Devices

### Hardware

- **ESP32**
- **Displays**
    - ST7789
- **Touch Controllers**
    - CST328

Other controllers may work but are currently untested.

### Platforms

- **Bridge**: Linux
- **Agents**: Linux, Windows

## Runtime & Build Requirements

- **Bridge / Agents**
    - .NET 9.0 runtime

- **Device**
    - ESP-IDF v6.0
    - C++23-compatible toolchain

This repository uses Git submodules.

## TODO

- [ ] Improve agent discovery and connection handling
- [ ] Document full wiring and hardware setup
- [ ] Expand supported displays and touch controllers
- [ ] Improve Bluetooth RFCOMM setup documentation
- [ ] Add basic CI for bridge and agent components