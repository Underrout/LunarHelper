#pragma once

#if LM_VERSION == 330
#include "Addresses/Addresses330.h"
#elif LM_VERSION == 331
#include "Addresses/Addresses331.h"
#elif LM_VERSION == 332
#include "Addresses/Addresses331.h"
#elif LM_VERSION == 333
#include "Addresses/Addresses331.h"
#endif

constexpr size_t COMMENT_FIELD_SFC_ROM_OFFSET = 0x7F120;
constexpr size_t COMMENT_FIELD_SMC_ROM_OFFSET = 0x7F320;
