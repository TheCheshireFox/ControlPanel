#!/bin/env python3

import pytest
from pytest_embedded_idf.dut import IdfDut
from pytest_embedded_idf.utils import idf_parametrize

@idf_parametrize('target', ['esp32s3'], indirect=['target'])
@pytest.mark.generic
def test_unity_single_dut(dut: IdfDut) -> None:
    dut.run_all_single_board_cases()