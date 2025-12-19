#!/bin/env python3

import re
import subprocess as sp

ELF='./build/ControlPanel.Device.elf'

def main():
    bt = input('BT: ')
    m = re.match(r'^.*Backtrace:(.+)$', bt)
    
    if not m:
        print('No backtrace')
        return

    addrs = list(filter(lambda x: len(x) > 0, (x.split(':')[0] for x in m.group(1).split(' '))))

    sp.check_call(['xtensa-esp32-elf-addr2line', '-pfiaC', '-e', ELF] + addrs)

main()