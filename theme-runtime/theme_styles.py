# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: theme_styles.py
# Purpose: Defines fixed theme identity colors and backdrop treatment choices.
#
# VibeWare: Human intent, AI execution, and plenty of tokens
# Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
#
# Created with AI: OpenAI Codex; extracted existing first-party source; 2026-10-27.
# Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
# Human guidance: Umberto Giacobbi; maintainability and standard file headers.
#
# Copyright (c) 2026 Umberto Giacobbi
# SPDX-License-Identifier: MIT
#
# VBWR E

"""Fixed theme identity colors and cached backdrop treatment choices."""

# Each Codex composition has its own color identity. Wallpaper samples tint these
# anchors gently rather than giving all ten themes the same four colors.
CODEX_IDENTITIES = {
    "beacon": ("#111629", "#F0F2FC", ("#7395FF", "#B694EC", "#4DBDCA", "#D8ADD9")),
    "timeline": ("#271C29", "#FFF9F2", ("#DB9D8D", "#BB9ADB", "#C9AA91", "#F2C5B4")),
    "terminal": ("#071713", "#EEF8EE", ("#56BC94", "#77BBA7", "#2D8174", "#A8C797")),
    "focus": ("#291D31", "#FFF1F5", ("#C394D8", "#EB96BA", "#A494D2", "#E6B4AA")),
    "pulse": ("#081B2C", "#EFFAFF", ("#46BEE7", "#466BC8", "#A27DE3", "#64D6C5")),
    "approval": ("#201329", "#FFF4E8", ("#9778BC", "#5F467D", "#CC9C83", "#754E85")),
    "flow": ("#261F36", "#FFFBF5", ("#A28BE6", "#F29D96", "#C1A7ED", "#F3BAAB")),
    "quiet": ("#102521", "#F1F7F2", ("#8BBDA8", "#75A6A4", "#D1CCAA", "#94A0C1")),
    "dispatch": ("#10242F", "#F0F5F4", ("#63A4B8", "#CF9270", "#597BA5", "#99C1B2")),
    "chronicle": ("#2B241E", "#FCF5E9", ("#C3A070", "#A9B797", "#C69079", "#869D9B")),
}

SYSTEM_IDENTITIES = {
    "signal": ("#0D161C", "#F1F6F2", ("#7BE5C0", "#76D5E9", "#B6CF94", "#92A5B7")),
    "ledger": ("#25221D", "#F6F2E8", ("#DA986D", "#98B699", "#B4A290", "#D2B98B")),
    "orbit": ("#091D26", "#EEF7F4", ("#73DCE0", "#A7E0B5", "#D8B88B", "#93B8C2")),
    "terminal": ("#07150D", "#F0F6E9", ("#91DD8B", "#B4DCA0", "#69BDA0", "#94B584")),
    "thermal": ("#25150F", "#FFF0E6", ("#FF9F67", "#F7C16E", "#CB9680", "#C6C399")),
    "blueprint": ("#092849", "#EDF5FB", ("#8EDAF2", "#A8C9F9", "#90D8C4", "#A8BBD1")),
    "horizon": ("#211D2D", "#F8F1EA", ("#E6AEA1", "#9BC6BA", "#C7B5D8", "#D5BE9F")),
    "blocks": ("#111B38", "#F5F1E7", ("#7191FF", "#F4CF4D", "#A0CBB8", "#C3BDDD")),
    "quiet": ("#18231F", "#F3F5EF", ("#A6C3AE", "#9CBAB9", "#C6C8AF", "#ADB6B0")),
    "pulse": ("#090D15", "#F5F3F8", ("#63D8EA", "#EC97D4", "#B6C59B", "#A8AFC6")),
}

MEDIA_TREATMENTS = {
    "aurora": {"photo": True, "blur": 21, "dark": .24, "light": .48, "blob": .18},
    "editorial": {"photo": False, "blob": .50},
    "cinematic": {"photo": True, "blur": 5, "dark": .14, "light": .42, "blob": .05},
    "signal": {"photo": False, "blob": .27},
    "orbit": {"photo": True, "blur": 48, "dark": .28, "light": .44, "blob": .32},
    "prism": {"photo": False, "blob": .65},
    "ribbon": {"photo": False, "blob": .42},
    "nocturne": {"photo": True, "blur": 34, "dark": .34, "light": .50, "blob": .14},
    "halo": {"photo": False, "blob": .50},
    "duotone": {"photo": True, "blur": 40, "dark": .28, "light": .48, "blob": .25},
}
