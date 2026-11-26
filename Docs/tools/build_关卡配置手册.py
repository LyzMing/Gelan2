# -*- coding: utf-8 -*-
"""生成《关卡配置手册》docx。"""

import os
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

OUT = r"D:\UnityProjects\Gelan2\Docs\关卡配置手册.docx"

FONT = "微软雅黑"
MONO = "Consolas"

ACCENT = RGBColor(0x1F, 0x4E, 0x79)
ACCENT2 = RGBColor(0x2E, 0x74, 0xB5)
WARN = RGBColor(0xC0, 0x50, 0x4D)
OK = RGBColor(0x38, 0x76, 0x1D)
GREY = RGBColor(0x59, 0x59, 0x59)


def set_run_font(run, name=FONT, size=None, bold=None, color=None, italic=None):
    run.font.name = name
    rpr = run._element.get_or_add_rPr()
    rfonts = rpr.find(qn("w:rFonts"))
    if rfonts is None:
        rfonts = OxmlElement("w:rFonts")
        rpr.append(rfonts)
    rfonts.set(qn("w:eastAsia"), name)
    rfonts.set(qn("w:ascii"), name)
    rfonts.set(qn("w:hAnsi"), name)
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.font.bold = bold
    if italic is not None:
        run.font.italic = italic
    if color is not None:
        run.font.color.rgb = color


def style_document(doc):
    normal = doc.styles["Normal"]
    normal.font.name = FONT
    normal.font.size = Pt(10.5)
    normal.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
    normal.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.LEFT
    normal.paragraph_format.space_after = Pt(6)

    for i, size in ((1, 16), (2, 13.5), (3, 11.5)):
        st = doc.styles[f"Heading {i}"]
        st.font.name = FONT
        st.font.size = Pt(size)
        st.font.color.rgb = ACCENT if i == 1 else ACCENT2
        st.font.bold = True
        st.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)
        st.paragraph_format.space_before = Pt(14 if i == 1 else 10)
        st.paragraph_format.space_after = Pt(6)
        st.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.LEFT

    sec = doc.sections[0]
    sec.page_width = Cm(21.0)
    sec.page_height = Cm(29.7)
    sec.left_margin = Cm(2.2)
    sec.right_margin = Cm(2.2)
    sec.top_margin = Cm(2.0)
    sec.bottom_margin = Cm(2.0)


CONTENT_W = 16.2   # 正文可用宽度（cm）= 21 - 2.2 - 2.2，留一点余量


def h1(doc, text):
    p = doc.add_heading(level=1)
    r = p.add_run(text)
    set_run_font(r, size=16, bold=True, color=ACCENT)
    return p


def h2(doc, text):
    p = doc.add_heading(level=2)
    r = p.add_run(text)
    set_run_font(r, size=13.5, bold=True, color=ACCENT2)
    return p


def h3(doc, text):
    p = doc.add_heading(level=3)
    r = p.add_run(text)
    set_run_font(r, size=11.5, bold=True, color=ACCENT2)
    return p


def para(doc, text="", size=10.5, bold=False, color=None, space_after=6, indent=0):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(space_after)
    if indent:
        p.paragraph_format.left_indent = Cm(indent)
    if text:
        r = p.add_run(text)
        set_run_font(r, size=size, bold=bold, color=color)
    return p


def rich(doc, parts, size=10.5, space_after=6, indent=0):
    """parts: [(text, {bold/color/mono}), ...]"""
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(space_after)
    if indent:
        p.paragraph_format.left_indent = Cm(indent)
    for text, style in parts:
        r = p.add_run(text)
        set_run_font(
            r,
            name=MONO if style.get("mono") else FONT,
            size=size - 1 if style.get("mono") else size,
            bold=style.get("bold", False),
            color=style.get("color"),
        )
    return p


def note(doc, text, kind="warn"):
    color = {"warn": WARN, "ok": OK, "info": GREY}[kind]
    mark = {"warn": "⚠ ", "ok": "✔ ", "info": "· "}[kind]
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(6)
    p.paragraph_format.left_indent = Cm(0.4)
    r = p.add_run(mark + text)
    set_run_font(r, size=10, color=color, bold=(kind == "warn"))
    return p


def bullet(doc, text, size=10.5, level=0):
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.left_indent = Cm(0.75 + 0.6 * level)
    r = p.add_run(text)
    set_run_font(r, size=size)
    return p


def mono_block(doc, lines, size=9):
    for line in lines:
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(1)
        p.paragraph_format.left_indent = Cm(0.5)
        r = p.add_run(line)
        set_run_font(r, name=MONO, size=size, color=RGBColor(0x33, 0x33, 0x33))


def shade(cell, hex_color):
    tc_pr = cell._tc.get_or_add_tcPr()
    el = OxmlElement("w:shd")
    el.set(qn("w:val"), "clear")
    el.set(qn("w:color"), "auto")
    el.set(qn("w:fill"), hex_color)
    tc_pr.append(el)


def table(doc, header, rows, widths=None, size=9.5):
    n = len(header)

    # 把调用方给的相对宽度按正文宽度等比缩放，保证永远不溢出
    if widths is None:
        widths = [CONTENT_W / n] * n
    else:
        scale = CONTENT_W / float(sum(widths))
        widths = [w * scale for w in widths]

    t = doc.add_table(rows=1, cols=n)
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.LEFT
    t.autofit = False          # 这一句会写入正确位置的 w:tblLayout fixed

    # 只改已存在的 w:tblW（不能 append，否则 tblPr 里出现重复元素，Word/LO 会退回自动布局）
    tbl_pr = t._tbl.tblPr
    tbl_w = tbl_pr.find(qn("w:tblW"))

    if tbl_w is None:
        tbl_w = OxmlElement("w:tblW")
        tbl_pr.insert(0, tbl_w)

    tbl_w.set(qn("w:type"), "pct")
    tbl_w.set(qn("w:w"), "5000")          # 100% 正文宽度，任何渲染器都不会溢出

    for i, w in enumerate(widths):
        t.columns[i].width = Cm(w)

    hdr = t.rows[0].cells
    for i, text in enumerate(header):
        hdr[i].text = ""
        hdr[i].width = Cm(widths[i])
        p = hdr[i].paragraphs[0]
        p.paragraph_format.space_after = Pt(0)
        r = p.add_run(text)
        set_run_font(r, size=size, bold=True, color=RGBColor(0xFF, 0xFF, 0xFF))
        shade(hdr[i], "1F4E79")

    for row in rows:
        cells = t.add_row().cells
        for i, text in enumerate(row):
            cells[i].text = ""
            cells[i].width = Cm(widths[i])
            p = cells[i].paragraphs[0]
            p.paragraph_format.space_after = Pt(0)
            mono = isinstance(text, tuple)
            value = text[0] if mono else text
            r = p.add_run(str(value))
            set_run_font(r, name=MONO if mono else FONT, size=size - 0.5 if mono else size)

    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return t


def build():
    doc = Document()
    style_document(doc)

    # ---------------- 封面 ----------------
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before = Pt(60)
    r = p.add_run("关卡配置手册")
    set_run_font(r, size=30, bold=True, color=ACCENT)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run("拼图 · 对话 · 探索 · 关卡串联")
    set_run_font(r, size=13, color=GREY)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run("Gelan2  /  Unity 2022.3.62f3c1")
    set_run_font(r, size=10.5, color=GREY)

    doc.add_paragraph()

    note(doc, "这份文档面向策划：只讲怎么在 Unity 里配一关，不讲代码实现。", "info")
    note(doc, "所有配置都在 Inspector 里完成，不需要写代码、不需要改脚本。", "ok")

    # ---------------- 目录 ----------------
    h1(doc, "目录")
    toc = [
        "1  五分钟配一关",
        "2  基础概念",
        "3  关卡资产 LevelDefinition",
        "4  拼图配置",
        "5  拼图布局编辑器",
        "6  对话（字幕）系统",
        "7  探索场景配置",
        "8  关卡串联",
        "9  编辑器工具速查",
        "10  校验与排错",
        "11  美术素材规范",
        "12  发布前检查清单",
        "13  常见问题 FAQ",
    ]
    for line in toc:
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(2)
        p.paragraph_format.left_indent = Cm(0.5)
        r = p.add_run(line)
        set_run_font(r, size=10.5)

    # ---------------- 1 ----------------
    doc.add_page_break()
    h1(doc, "1  五分钟配一关")

    para(doc, "一个新关卡要做的事情，按顺序就这七步：")

    table(
        doc,
        ["步骤", "做什么", "在哪做"],
        [
            ["1", "新建关卡资产，填 levelId / 显示名", "Project 右键 → Create → Remiehzla → Level Definition"],
            ["2", "填拼图：图片、格数、允许的操作", "选中关卡资产 → Inspector"],
            ["3", "摆初始布局 + 配正解", "菜单 工具 → 拼图布局编辑器"],
            ["4", "（可选）配拼图开场对话", "关卡资产的 Puzzle Dialogue"],
            ["5", "搭探索场景：背景、道具、角色", "新建场景，见第 7 章"],
            ["6", "把拼完后要进的场景名填上", "关卡资产的 Explore Scene Name"],
            ["7", "跑一次校验，然后 Play 走一遍", "工具 → 校验关卡定义 / 校验探索场景"],
        ],
        widths=[1.2, 8.0, 6.5],
    )

    note(doc, "新建的场景必须拖进 File → Build Settings → Scenes In Build，否则加载会失败。", "warn")

    # ---------------- 2 ----------------
    h1(doc, "2  基础概念")

    h2(doc, "2.1  三个资产")
    table(
        doc,
        ["资产", "作用", "怎么建"],
        [
            ["LevelDefinition（关卡）", "一关的全部配置：拼图、对话、要跳的场景、下一关", "右键 Create → Remiehzla → Level Definition"],
            ["DialogueAsset（对话）", "一段字幕的内容、奖励、播完后的行为", "右键 Create → 剧情 → 对话"],
            ["Texture2D（拼图整图）", "被切成格子的那张正方形图", "直接拖图片进 Project"],
        ],
        widths=[4.6, 7.4, 5.0],
    )

    h2(doc, "2.2  一关的完整流程")
    mono_block(
        doc,
        [
            "开始",
            "  ↓",
            "拼图阶段（可选开场对话 → 播完解锁 → 玩家拼图 → 点「确定」）",
            "  ↓ 判定通过，拿到 solutionId（normal / hidden_xxx）",
            "加载该解对应的探索场景（不同解可以进不同场景）",
            "  ↓",
            "探索阶段（走路 / 进入区域触发对话 / 按 E 交互 / 捡东西）",
            "  ↓ 走到门按 E，或者某段对话播完自动过关",
            "下一关的拼图阶段 ……",
        ],
    )

    h2(doc, "2.3  一个重要的前提")
    para(doc, "游戏用的是「拼图场景 + 每关一个探索场景」的结构：")
    bullet(doc, "拼图场景只有一个（Puzzle.unity），所有关卡复用，靠关卡资产换图换格数。")
    bullet(doc, "探索场景每关一个，名字要和关卡资产里的 Explore Scene Name 完全一致。")
    note(doc, "改场景名的时候，记得同时改关卡资产里的名字，并重新检查 Build Settings。", "warn")

    # ---------------- 3 ----------------
    h1(doc, "3  关卡资产 LevelDefinition")

    para(doc, "选中关卡资产，Inspector 里从上到下是这个结构：")

    table(
        doc,
        ["字段", "说明", "怎么填"],
        [
            [("levelId", {}), "关卡唯一 id，也是存档键、收集品 id 前缀", "L01_HOME 这种格式，全局唯一"],
            ["Display Name", "关卡显示名", "家"],
            [("Puzzle", {}), "拼图配置（见第 4 章）", "展开填"],
            [("Puzzle Dialogue", {}), "进入拼图阶段时自动播的字幕，播完才能操作拼图", "拖一个对话资产进来，可留空"],
            [("Explore Scene Name", {}), "拼图通过后加载的探索场景名", "必须和场景文件名一致"],
            [("Next Level", {}), "下一关的关卡资产", "拖下一个 LevelDefinition，最后一关留空"],
            [("Collectibles", {}), "本关可拾取物的 id 列表", "L01_PHOTO 这种格式"],
        ],
        widths=[4.6, 8.2, 4.2],
    )

    note(doc, "levelId 和 Collectibles 里的 id 建议都用「关卡前缀_物品名」的格式，排查问题时一眼能认出来。", "info")

    # ---------------- 4 ----------------
    h1(doc, "4  拼图配置")

    h2(doc, "4.1  整图")
    table(
        doc,
        ["字段", "要求"],
        [
            [("Image", {}), "正方形。边长建议能被格数整除（例如 1080 适合 3×3 / 4×4 / 6×6）"],
            [("Grid Size", {}), "几乘几，2 ~ 8。逐关递增：2×2 → 3×3 → 4×4 …"],
        ],
        widths=[4.0, 13.0],
    )
    note(doc, "图片不是正方形会变形，边长不能整除时边缘会错位——校验工具会提示。", "warn")

    h2(doc, "4.2  初始布局与正解")
    para(doc, "这两个都用「工具 → 拼图布局编辑器」来摆，不在 Inspector 里手填。")
    bullet(doc, "Initial（初始布局）：玩家一进关看到的样子，通常是打乱的。")
    bullet(doc, "Solutions（正解）：至少配一个普通解；可以额外配隐藏解。")
    note(doc, "初始布局不是「每块各就各位」是正常的（本来就该打乱），但必须包含完整的图块集合，详见 4.6。", "info")

    h2(doc, "4.3  正解的字段")
    table(
        doc,
        ["字段", "说明"],
        [
            [("solutionId", {}), "传给探索阶段的分支键。普通解写 normal，隐藏解写 hidden_xxx"],
            [("Is Hidden（隐藏解）", {}), "勾上 = 不参与常规通关，用于解锁隐藏内容"],
            [("Explore Scene Name（探索场景）", {}), "这个解跳哪个探索场景。留空 = 用关卡默认的"],
        ],
        widths=[6.6, 10.4],
    )
    note(doc, "判定时会先比普通解、再比隐藏解，所以隐藏解不会抢掉常规通关。", "ok")

    h3(doc, "每个解跳不同的探索场景")
    para(doc, "典型用法：解出隐藏解之后，进入一个专属场景。")
    mono_block(
        doc,
        [
            "普通解  solutionId = normal          探索场景 = （留空，用关卡默认）",
            "隐藏解  solutionId = hidden_twins   探索场景 = L03_Explore_Hidden",
        ],
    )
    note(doc, "这个专属场景同样要加进 Build Settings，名字一个字都不能差。", "warn")

    h2(doc, "4.4  允许的操作 Allowed Ops")

    para(doc, "逐关勾选，勾了哪些玩家就能用哪些：")
    table(
        doc,
        ["操作", "玩家怎么用", "默认"],
        [
            ["点选", "鼠标左键点格子", "勾选"],
            ["平移（方向键交换相邻格）", "↑ ↓ ← →", "勾选"],
            ["旋转（H/K）", "K 顺时针 90°，H 逆时针 90°", "不勾"],
            ["水平翻转（J）", "J 左右镜像", "不勾"],
            ["拖动", "按住格子拖到另一格交换", "不勾"],
            ["复制（C）", "C 复制，移动鼠标预览，左键放下覆盖，Esc 取消", "不勾"],
            ["撤回（Z）", "Z 撤销上一步", "不勾"],
            ["错乱块", "数据里存在无法正常显示/判定的块（配合布局编辑器标记）", "不勾"],
        ],
        widths=[6.4, 8.6, 2.0],
    )

    note(doc, "「复制」是唯一能让玩家做出重复图块的操作。初始布局里有重复块时，通常需要勾上它。", "info")

    h2(doc, "4.5  步数与打乱")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Count Rotate As Step", {}), "勾上 = 旋转也算一步（影响步数统计）"],
            [("Move Limit", {}), "步数上限。填 0 = 不限"],
            [("Allow Shuffle In Game", {}), "勾上 = 玩家按 R 可以重新随机打乱。正式关卡建议保持关闭"],
        ],
        widths=[6.6, 10.4],
    )
    note(doc, "「重新打乱」会生成完整的图块排列，会把你故意放的重复块洗掉。正式关卡不要开。", "warn")

    h2(doc, "4.6  关于重复图块（重要）")
    para(doc, "初始布局允许出现重复图块、也允许缺某一块（可以做干扰）。但校验工具会判断这关还能不能解开：")
    table(
        doc,
        ["情况", "校验结果"],
        [
            ["图块编号越界", "错误：必须修"],
            ["有重复/缺块 + 开了复制操作", "警告：玩家可以自己复制补齐，没问题"],
            ["有重复/缺块 + 正解里也有重复块", "警告：确认这样能解开即可"],
            ["有重复/缺块 + 没开复制 + 正解没重复", "错误：玩家拿不到缺的那块，拼不出正解"],
        ],
        widths=[8.6, 8.4],
    )

    # ---------------- 5 ----------------
    h1(doc, "5  拼图布局编辑器")

    para(doc, "菜单：工具 → 拼图布局编辑器。")

    h2(doc, "5.1  基本操作")
    table(
        doc,
        ["想做什么", "怎么做"],
        [
            ["选中一格", "点它（再点一次取消选中）"],
            ["交换两块", "先点一格，再点另一格"],
            ["切换编辑目标", "顶部下拉：初始布局 / 各个正解"],
            ["旋转选中块", "↺ 逆时针 / ↻ 顺时针 按钮"],
            ["翻转选中块", "⇄ 翻转 按钮"],
            ["标记错乱块", "标记为错乱块 按钮（显示成 X）"],
            ["批量摆正", "全部摆正 按钮（保留位置，清掉旋转和翻转）"],
            ["一键回到正解形态", "重置为正解形态 按钮（每块回到自己格子且正立）"],
            ["随机打乱位置", "随机打乱位置 按钮"],
        ],
        widths=[5.6, 11.4],
    )

    h2(doc, "5.2  复制粘贴")
    table(
        doc,
        ["按钮", "快捷键", "效果"],
        [
            ["复制", "Ctrl + C", "复制选中格的状态"],
            ["粘贴", "Ctrl + V", "连同图块编号一起贴过去（会出现重复图块）"],
            ["只粘朝向", "Ctrl + Shift + V", "只复制旋转和翻转，保留目标格是哪一块"],
        ],
        widths=[3.6, 4.4, 9.0],
    )
    note(doc, "批量摆同一种朝向时用「只粘朝向」，不会破坏图块的唯一性。", "ok")

    h2(doc, "5.3  注意事项")
    bullet(doc, "改动会立即写回关卡资产，Ctrl+Z 可以撤销。")
    bullet(doc, "编辑器的格子上写着 #格子号 ← 图块号，右上角的小标记表示旋转圈数和是否翻转。")
    bullet(doc, "窗口底部会实时跑一遍校验，有错误会直接显示。")

    # ---------------- 6 ----------------
    h1(doc, "6  对话（字幕）系统")

    para(doc, "所有对话都是屏幕下方的字幕：逐字打出、一句播完停一会儿自动进下一句，玩家不需要按键。")

    h2(doc, "6.1  对话资产字段")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Memo", {}), "调试用的备注名，不会显示在游戏里"],
            [("Lines", {}), "按顺序播放的字幕。每行有：正文 / 打字速度 / 播完停留"],
            [("Reward Item Id", {}), "触发这段对话就获得的物品 id，留空 = 没有奖励"],
            [("Reward Label", {}), "提示里显示的名字，例如 老照片"],
            [("Go To Next Level When Finished", {}), "勾上 = 播完自动进入下一关（和走到门一样）"],
        ],
        widths=[6.6, 10.4],
    )

    h3(doc, "每句台词的三个字段")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Text", {}), "字幕正文。可以留空，用来做停顿"],
            [("Chars Per Second", {}), "这句的打字速度（字/秒）。填 0 = 用默认的 26"],
            [("Hold After", {}), "这句播完后停留多久再进下一句（秒）。填 0 = 用默认的 1.1"],
        ],
        widths=[5.6, 11.4],
    )

    h2(doc, "6.2  两种触发方式")
    table(
        doc,
        ["组件", "触发方式", "播放时玩家能走吗"],
        [
            [("Dialogue Zone", {}), "走进区域自动触发", "能走"],
            [("Dialogue Interactable", {}), "靠近显示提示，按 E 触发", "不能走（会锁住）"],
        ],
        widths=[5.0, 7.0, 5.0],
    )

    h3(doc, "区域触发 Dialogue Zone")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Dialogue", {}), "拖对话资产"],
            [("One Shot", {}), "只能触发一次（默认勾选）"],
            [("Lock Movement", {}), "进入时是否锁住玩家移动，区域触发一般不用勾"],
            [("Required Item Id", {}), "需要先拿到的物品 id，留空 = 无条件（见 6.5）"],
            [("Size", {}), "触发范围大小。也可以给它加一个 BoxCollider2D 直接拖范围（不用勾 Trigger）"],
        ],
        widths=[5.6, 11.4],
    )
    note(doc, "选中区域物体会显示一个黄色线框，就是实际触发范围。", "info")

    h3(doc, "互动触发 Dialogue Interactable")
    para(doc, "挂在物品或家具上，玩家走近会看到 [E] 查看，按 E 播字幕。")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Dialogue", {}), "拖对话资产"],
            [("One Shot", {}), "只能触发一次"],
            [("Lock Movement", {}), "播放期间禁用玩家移动（默认勾选）"],
            [("Prompt Override", {}), "按 E 时显示的文字，默认「查看」"],
            [("Required Item Id", {}), "需要先拿到的物品 id（见 6.5）"],
        ],
        widths=[5.6, 11.4],
    )

    h2(doc, "6.3  播放规则")
    bullet(doc, "同一时间只会播一段对话。正在播的时候，别的区域/物品都不会触发。")
    bullet(doc, "被挡住的那一次不算「已触发」——等上一段播完，重新走进区域还能正常触发。")
    bullet(doc, "播放期间，所有可交互物（门、拾取、对话）都会锁住，播完才恢复。")
    bullet(doc, "打字机效果 + 自动推进，不需要玩家按键。")

    h2(doc, "6.4  奖励物品")
    para(doc, "对话资产里填 Reward Item Id / Reward Label：")
    bullet(doc, "只要对话被触发，物品立刻到手（不需要等播完）。")
    bullet(doc, "屏幕上方弹出「获得：老照片」。")
    note(doc, "同一个物品 id 只会获得一次。", "info")

    h2(doc, "6.5  条件触发")
    para(doc, "两种触发方式都支持「要先拿到某物品」：")

    h3(doc, "互动类（有提示）")
    mono_block(
        doc,
        ["Required Item Id = L01_KEY"],
    )
    bullet(doc, "提示照常显示（[E] 查看）。")
    bullet(doc, "条件不满足时按 E，只会弹一句「暂未满足条件」，不播对话、不开门。")
    bullet(doc, "提示文字可以在 Condition Locked Hint 里改。")

    h3(doc, "区域类（静默）")
    bullet(doc, "条件不满足：走进区域什么都不会发生。")
    bullet(doc, "拿到物品之后，必须走出去再重新进来才会触发（站在里面等不会触发）。")

    h2(doc, "6.6  想让字幕更好读")
    para(doc, "在探索场景里建一个空物体，挂 Subtitle Player，就能在 Inspector 调：")
    table(
        doc,
        ["参数", "默认", "说明"],
        [
            [("Chars Per Second", {}), "26", "打字速度"],
            [("Hold After Line", {}), "1.1", "一句播完停多久"],
            [("Font Size", {}), "28", "字号"],
            [("Bar Height", {}), "150", "字幕条高度"],
            [("Bottom Margin", {}), "0", "离屏幕底边的距离"],
            [("Bar Color", {}), "黑 66%", "背景条颜色，alpha 设 0 就是不要背景"],
        ],
        widths=[5.2, 3.0, 8.8],
    )
    note(doc, "不放这个物体也能跑（第一次触发时自动创建，用默认值），但那样就调不了参数。", "info")

    # ---------------- 7 ----------------
    h1(doc, "7  探索场景配置")

    h2(doc, "7.1  场景必备")
    table(
        doc,
        ["物体", "组件", "作用"],
        [
            ["任意空物体（建议叫 GameFlow）", ("ExploreSceneController", {}), "探索场景的入口：接拼图结果、处理过关、切下一关"],
            ["玩家", ("TempPlayerController", {}), "移动 + 撞墙（目前是临时实现）"],
            ["玩家", ("Player Animator", {}), "四向走路动画"],
            ["玩家", ("TempInteractor", {}), "交互检测 + [E] 提示（提示 UI 不填会自动生成）"],
            ["背景图", "Sprite Renderer", "Sorting Order 设 -1000，不要挂 YSort"],
            ["道具", ("YSort", {}), "决定玩家走到后面时谁挡谁"],
        ],
        widths=[6.0, 5.6, 5.4],
    )

    note(doc, "ExploreSceneController 的 Level 字段是「我现在在打哪一关」，不是「下一关」。"
              "单独调试场景时才填，正常流程会从拼图阶段自动传过来。", "warn")

    h2(doc, "7.2  角色动画")
    table(
        doc,
        ["字段", "怎么填"],
        [
            [("Front / Back / Left / Right Sheet", {}), "四张朝向图，每张是同一朝向 4 帧横排"],
            [("Pixels Per Unit", {}), "25，必须和背景一致"],
            [("Frames Per Second", {}), "8（4 帧 × 8fps = 0.5 秒一个走路循环）"],
            [("Pivot", {}), "(0.5, 0) 底部中点，这样脚点才对得上 YSort"],
        ],
        widths=[6.6, 10.4],
    )
    note(doc, "贴图必须勾 Read/Write Enabled，用 工具 → 美术 → 4 一键开启，否则角色不显示。", "warn")
    note(doc, "Player 的 localScale 保持 (1,1,1)。之前为了让占位方块显眼调大过的话记得改回来。", "warn")

    h2(doc, "7.3  遮挡排序 YSort")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Foot Offset", {}), "脚点相对物体原点的偏移，pivot 没放对时用来补"],
            [("Order Per Unit", {}), "100，一般不用改"],
            [("Sort Bias", {}), "手动微调。正数永远在前（吊灯），负数永远在后（地毯）"],
        ],
        widths=[5.6, 11.4],
    )

    h3(doc, "画法规矩")
    bullet(doc, "pivot 放底部中点 —— transform.position 就是脚点。")
    bullet(doc, "要绕到后面的物件别画成实心块：桌子要有腿、柜子要有空隙，玩家站后面才只被挡一部分。")
    bullet(doc, "一个物件由多张图组成时，在根节点加 YSort + SortingGroup，组内各层自己排前后。")
    bullet(doc, "地毯、挂墙的画、吊灯不要挂 YSort，用固定 Sorting Order 或 Sort Bias。")
    note(doc, "YSort 只比较一个脚点，所以换层是「整块」切换，没有渐变——这是设计如此，靠画法规避。", "info")

    h2(doc, "7.4  可交互物一览")
    table(
        doc,
        ["组件", "用途", "关键字段"],
        [
            [("Pickup Interactable", {}), "捡东西", ("Item Id", {}), ],
            [("Door Interactable", {}), "过关的门", ("Target Scene Name", {})],
            [("Trigger Interactable", {}), "剧情点 / 机关", ("Use As Flag", {})],
            [("Dialogue Interactable", {}), "看东西 / 读信", ("Dialogue", {})],
        ],
        widths=[5.4, 4.6, 7.0],
    )
    para(doc, "所有可交互物都继承同一套通用字段：")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Interactable Id", {}), "埋点 / 条件用的 id，可留空"],
            [("Prompt Override", {}), "按 E 时显示的文字，留空用默认值"],
            [("Highlight Target", {}), "高亮锚点，留空 = 用自己"],
            [("Start Disabled", {}), "初始就禁用交互"],
            [("Required Item Id", {}), "需要先拿到的物品 id（见 6.5）"],
            [("Condition Locked Hint", {}), "条件不满足时的提示文字"],
        ],
        widths=[5.6, 11.4],
    )

    h2(doc, "7.5  按拼图解法显示不同内容")
    para(doc, "同一个探索场景里，想让某些东西只在特定解出现时，挂 ExploreVariant：")
    table(
        doc,
        ["字段", "说明"],
        [
            [("Show For Solutions", {}), "只在这些 solutionId 下显示，留空 = 不限制"],
            [("Hide For Solutions", {}), "在这些 solutionId 下隐藏（优先于显示）"],
            [("Destroy When Hidden", {}), "隐藏时直接销毁而不是只禁用"],
        ],
        widths=[6.0, 11.0],
    )

    h2(doc, "7.6  怎么过关")
    table(
        doc,
        ["方式", "怎么配"],
        [
            ["走到门按 E", "门上挂 Door Interactable"],
            ["某段对话播完自动过关", "对话资产勾上 Go To Next Level When Finished"],
        ],
        widths=[6.0, 11.0],
    )
    para(doc, "两种方式最终都走同一套流程：结束本关 → 播下一关的拼图场景。")
    note(doc, "过关需要关卡资产的 Next Level 指向下一关；最后一关留空，走到门会提示「当作最后一关处理」。", "warn")

    # ---------------- 8 ----------------
    h1(doc, "8  关卡串联")

    mono_block(
        doc,
        [
            "L01_HOME                          ← 关卡资产",
            "  ├─ puzzle.image                  第一关的拼图整图",
            "  ├─ puzzle.solutions[0]  normal   exploreSceneName 留空",
            "  ├─ puzzle.solutions[1]  hidden    exploreSceneName = L01_Explore_Hidden",
            "  ├─ exploreSceneName = L01_Explore",
            "  └─ nextLevel      → L02_SCHOOL   ← 指向下一个关卡资产",
            "",
            "L02_SCHOOL",
            "  ├─ exploreSceneName = L02_Explore",
            "  └─ nextLevel      → L03_xxx",
            "",
            "... 最后一关 nextLevel 留空",
        ],
    )

    table(
        doc,
        ["要检查的", "在哪看"],
        [
            ["所有用到的场景都在 Build Settings 里", "File → Build Settings"],
            ["exploreSceneName 和场景文件名一致", "关卡资产 / 每个解的探索场景"],
            ["nextLevel 串成一条链，最后一关留空", "各关卡资产"],
        ],
        widths=[8.0, 9.0],
    )

    # ---------------- 9 ----------------
    h1(doc, "9  编辑器工具速查")

    table(
        doc,
        ["菜单", "作用"],
        [
            ["工具 → 拼图布局编辑器", "摆初始布局、配正解、复制粘贴、实时校验"],
            ["工具 → 校验关卡定义", "检查当前选中的关卡资产（拼图配置、正解、场景名）"],
            ["工具 → 校验探索场景", "检查当前打开的探索场景（收集品 id、门的配置等）"],
            ["工具 → 清除进度存档", "清空收集/通关记录（调试用）"],
            ["工具 → 美术 → 1. 设为像素画导入设置", "一键设置 Sprite / PPU 25 / Point / 不压缩"],
            ["工具 → 美术 → 2. 把 Pivot 设到内容底部中点", "批量修 pivot，让脚点对上 YSort"],
            ["工具 → 美术 → 3. 检查透明留白", "找出留白过多的图，方便让美术裁紧"],
            ["工具 → 美术 → 4. 开启贴图 Read/Write", "角色动画图必须，否则角色不显示"],
            ["CONTEXT → LevelDefinition → 校验本关卡", "在关卡资产上右键也能校验"],
        ],
        widths=[7.4, 9.6],
    )

    # ---------------- 10 ----------------
    h1(doc, "10  校验与排错")

    h2(doc, "10.1  常见报错对照")
    table(
        doc,
        ["Console 提示", "原因", "怎么修"],
        [
            ["场景「xxx」没有加入 Build Settings", "场景没拖进构建列表", "File → Build Settings → Add Open Scenes"],
            ["还没有配置下一关 / Next Level 为空", "当前关卡是最后一关，或 nextLevel 没填", "填 Next Level，或确认这是最后一关"],
            ["本场景 ExploreSceneController 的 Level 填的是下一关", "Level 字段填错了", "填「本关」，不是下一关"],
            ["关卡没有可用的初始布局，已退回随机打乱", "初始布局没摆或格数不对", "用拼图布局编辑器摆一下"],
            ["拼图环节的开场对话，拼图暂时锁定", "正常提示", "播完自动解锁"],
            ["条件未满足，需要物品 xxx", "互动条件没满足", "正常行为，先让玩家拿到物品"],
            ["无法加载场景（找不到 SceneTransitionManager）", "转场系统缺失", "直接硬切兜底，不影响流程"],
        ],
        widths=[7.0, 5.6, 4.4],
    )

    h2(doc, "10.2  玩法层面的自查")
    bullet(doc, "拼图能拼出来吗？（初始布局的图块集合是否完整 / 正解是否可达）")
    bullet(doc, "拼完之后进的是对的场景吗？（每个解的探索场景名）")
    bullet(doc, "该触发的对话都触发了吗？（区域范围够不够大、itemId 有没有对上）")
    bullet(doc, "该锁的互动锁住了吗？（对话播放期间不响应 E）")
    bullet(doc, "走到门能进下一关吗？（nextLevel 串上了吗）")

    # ---------------- 11 ----------------
    h1(doc, "11  美术素材规范")

    table(
        doc,
        ["设置", "值", "原因"],
        [
            ["Texture Type", "Sprite (2D and UI)", "否则拖不进场景"],
            ["Pixels Per Unit", "25", "255px 背景 = 10.2 世界单位，36px 角色 = 1.44"],
            ["Filter Mode", "Point (no filter)", "像素画必须关过滤"],
            ["Compression", "None", "压缩会出色块"],
            ["Mipmap", "关闭", "2D 用不到"],
            ["Read/Write", "角色图开启", "运行时切帧需要"],
        ],
        widths=[4.4, 4.6, 8.0],
    )

    bullet(doc, "道具图请裁到内容边界再导出——留白会让 pivot 落在空白里，遮挡会算错。")
    bullet(doc, "命名建议用用途（bed_double / sofa / piano），不要用 IMG_18xx。")
    bullet(doc, "角色是 4 张 144×36 的走图，每张 4 帧。")

    # ---------------- 12 ----------------
    h1(doc, "12  发布前检查清单")

    items = [
        "关卡资产的 levelId 唯一，显示名正确",
        "拼图整图是正方形，边长能被格数整除",
        "初始布局已摆好，校验无错误",
        "至少有一个普通解；隐藏解（如果有）填好了专属场景",
        "Allowed Ops 只勾了这一关想给玩家的操作",
        "Move Limit 符合设计（0 = 不限）",
        "Allow Shuffle In Game 关闭（正式关卡）",
        "开场对话（如果有）已配置，且播完能解锁",
        "探索场景名填对，且已加进 Build Settings",
        "探索场景里有 ExploreSceneController",
        "玩家、背景、道具、YSort 都配好了",
        "可交互物的 Item Id 都在关卡资产的 Collectibles 里",
        "需要条件的互动填了 Required Item Id",
        "门的 Required Item Id / 提示都检查过",
        "Next Level 指向下一关（最后一关留空）",
        "完整跑一遍：拼图 → 探索 → 过关 → 下一关",
    ]
    for i, text in enumerate(items, 1):
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(3)
        p.paragraph_format.left_indent = Cm(0.6)
        r = p.add_run(f"☐  {i:00}. {text}")
        set_run_font(r, size=10.5)

    # ---------------- 13 ----------------
    h1(doc, "13  常见问题 FAQ")

    faq = [
        (
            "我一进探索场景角色就动不了 / 场景是空的",
            "如果是直接按 Play 打开探索场景，属于「单独调试」模式，没有拼图阶段传过来的数据。"
            "检查 ExploreSceneController 上有没有填 Level。",
        ),
        (
            "拼图拼完了没进探索场景，而是变成色块房间",
            "关卡资产的 Explore Scene Name 是空的，走了旧的同场景模式。把这个字段填上。",
        ),
        (
            "对话播完没自动进下一关",
            "对话资产的 Go To Next Level When Finished 没勾，或者关卡资产的 Next Level 是空的。",
        ),
        (
            "隐藏解拼出来了，但进的还是普通场景",
            "那个解的 Explore Scene Name 没填，或者填的场景名和 Build Settings 里不一致。",
        ),
        (
            "角色走进区域没触发对话",
            "① 区域范围太小（选中区域物体看黄色线框）② 这段对话已经触发过（One Shot）"
            "③ Required Item Id 还没拿到 ④ 上一段对话还在播。",
        ),
        (
            "按 E 只提示「暂未满足条件」",
            "这个互动配了 Required Item Id，玩家还没拿到对应物品。",
        ),
        (
            "收集状态每次 Play 都重置了",
            "目前故意不做磁盘存档，每次 Play 都是干净的，方便调试。",
        ),
        (
            "角色在拼图阶段也会动",
            "正式流程里拼图场景不加载角色。如果出现这种情况，检查是不是把角色放进了拼图场景。",
        ),
    ]

    for q, a in faq:
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(3)
        r = p.add_run("Q  " + q)
        set_run_font(r, size=10.5, bold=True, color=ACCENT2)
        p2 = doc.add_paragraph()
        p2.paragraph_format.space_after = Pt(10)
        p2.paragraph_format.left_indent = Cm(0.6)
        r2 = p2.add_run("A  " + a)
        set_run_font(r2, size=10.5)

    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(18)
    r = p.add_run("— 文档结束 —　配置中遇到文档没写的情况，直接在群里问，补充进下一版。")
    set_run_font(r, size=9.5, color=GREY)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    doc.save(OUT)
    print("saved:", OUT)


if __name__ == "__main__":
    build()
