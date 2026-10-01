"""
Вспомогательный модуль для генерации документов DOCX по стандарту СТП 01-2017 БГУИР.
"""
import docx
from docx.shared import Pt, Mm, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import parse_xml
from docx.oxml.ns import nsdecls

def create_stp_document():
    doc = docx.Document()
    
    # Настройка параметров страницы по СТП 01-2017:
    # Левое - 30 мм, Правое - 10 мм, Верхнее - 20 мм, Нижнее - 20 мм
    section = doc.sections[0]
    section.page_width = Mm(210)
    section.page_height = Mm(297)
    section.left_margin = Mm(30)
    section.right_margin = Mm(10)
    section.top_margin = Mm(20)
    section.bottom_margin = Mm(20)
    
    # Настройка колонтитулов: первая страница без номера
    section.different_first_page_header_footer = True
    
    # Нумерация страниц в правом верхнем углу (со 2 страницы)
    header = section.header
    header_p = header.paragraphs[0]
    header_p.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    run = header_p.add_run()
    run.font.name = 'Times New Roman'
    run.font.size = Pt(12)
    run.font.color.rgb = RGBColor(0, 0, 0)
    
    # Вставка поля номера страницы Word (PAGE)
    fldChar1 = parse_xml(r'<w:fldChar %s w:fldCharType="begin"/>' % nsdecls('w'))
    instrText = parse_xml(r'<w:instrText %s xml:space="preserve"> PAGE </w:instrText>' % nsdecls('w'))
    fldChar2 = parse_xml(r'<w:fldChar %s w:fldCharType="separate"/>' % nsdecls('w'))
    fldChar3 = parse_xml(r'<w:fldChar %s w:fldCharType="end"/>' % nsdecls('w'))
    run._r.append(fldChar1)
    run._r.append(instrText)
    run._r.append(fldChar2)
    run._r.append(fldChar3)
    
    # Настройка базового стиля "Normal"
    style_normal = doc.styles['Normal']
    font = style_normal.font
    font.name = 'Times New Roman'
    font.size = Pt(14)
    font.color.rgb = RGBColor(0, 0, 0)
    
    p_format = style_normal.paragraph_format
    p_format.line_spacing = 1.5
    p_format.space_before = Pt(0)
    p_format.space_after = Pt(0)
    p_format.first_line_indent = Cm(1.25)
    p_format.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    
    return doc

def add_title_page(doc, task_number, task_title, student_name="Белькевич Е. А.", year="2026"):
    """Создает титульный лист по стандарту БГУИР"""
    # Шапка университета
    headers = [
        "МИНИСТЕРСТВО ОБРАЗОВАНИЯ РЕСПУБЛИКИ БЕЛАРУСЬ",
        "УЧРЕЖДЕНИЕ ОБРАЗОВАНИЯ",
        "«БЕЛОРУССКИЙ ГОСУДАРСТВЕННЫЙ УНИВЕРСИТЕТ",
        "ИНФОРМАТИКИ И РАДИОЭЛЕКТРОНИКИ»",
        "Факультет компьютерных систем и сетей",
        "Кафедра программного обеспечения информационных технологий"
    ]
    
    for i, h_text in enumerate(headers):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.line_spacing = 1.15
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(2 if i < 4 else 4)
        run = p.add_run(h_text)
        run.font.name = 'Times New Roman'
        run.font.size = Pt(12 if i < 4 else 13)
        run.bold = (i in [2, 3])

    # Отступ перед названием работы
    p_space = doc.add_paragraph()
    p_space.paragraph_format.first_line_indent = Cm(0)
    p_space.paragraph_format.space_before = Pt(44)
    p_space.paragraph_format.space_after = Pt(0)

    # Заголовок работы
    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_title.paragraph_format.first_line_indent = Cm(0)
    p_title.paragraph_format.line_spacing = 1.2
    p_title.paragraph_format.space_after = Pt(8)
    run_otchet = p_title.add_run(f"ОТЧЕТ\nПО ПРАКТИЧЕСКОМУ ЗАДАНИЮ № {task_number}")
    run_otchet.font.name = 'Times New Roman'
    run_otchet.font.size = Pt(16)
    run_otchet.bold = True

    p_disc = doc.add_paragraph()
    p_disc.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_disc.paragraph_format.first_line_indent = Cm(0)
    p_disc.paragraph_format.line_spacing = 1.2
    p_disc.paragraph_format.space_after = Pt(10)
    run_disc = p_disc.add_run("по дисциплине «Теория практической реализации игр»")
    run_disc.font.name = 'Times New Roman'
    run_disc.font.size = Pt(14)
    run_disc.italic = True

    p_topic = doc.add_paragraph()
    p_topic.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_topic.paragraph_format.first_line_indent = Cm(0)
    p_topic.paragraph_format.line_spacing = 1.2
    p_topic.paragraph_format.space_after = Pt(0)
    run_topic = p_topic.add_run(f"на тему «{task_title}»")
    run_topic.font.name = 'Times New Roman'
    run_topic.font.size = Pt(14)
    run_topic.bold = True

    # Блок сведений об авторе и преподавателе
    p_space2 = doc.add_paragraph()
    p_space2.paragraph_format.first_line_indent = Cm(0)
    p_space2.paragraph_format.space_before = Pt(56)
    p_space2.paragraph_format.space_after = Pt(0)

    p_info = doc.add_paragraph()
    p_info.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_info.paragraph_format.left_indent = Cm(8.5)
    p_info.paragraph_format.first_line_indent = Cm(0)
    p_info.paragraph_format.line_spacing = 1.2
    p_info.paragraph_format.space_after = Pt(0)
    
    info_text = (
        f"Выполнил:\n"
        f"студент 4 курса\n"
        f"гр. 051001 (ПОИТ)\n"
        f"{student_name}\n\n"
        f"Проверил:\n"
        f"ст. преподаватель\n"
        f"________________"
    )
    run_info = p_info.add_run(info_text)
    run_info.font.name = 'Times New Roman'
    run_info.font.size = Pt(13)

    # Город и год внизу страницы
    p_space3 = doc.add_paragraph()
    p_space3.paragraph_format.first_line_indent = Cm(0)
    p_space3.paragraph_format.space_before = Pt(64)
    p_space3.paragraph_format.space_after = Pt(0)

    p_bottom = doc.add_paragraph()
    p_bottom.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_bottom.paragraph_format.first_line_indent = Cm(0)
    p_bottom.paragraph_format.line_spacing = 1.0
    p_bottom.paragraph_format.space_after = Pt(0)
    run_bottom = p_bottom.add_run(f"Минск {year}")
    run_bottom.font.name = 'Times New Roman'
    run_bottom.font.size = Pt(14)

    # Разрыв страницы после титульника
    doc.add_page_break()

def add_heading_1(doc, text):
    """Заголовок первого уровня по СТП 01-2017: Times New Roman 14 pt, полужирный, без точки в конце"""
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p.paragraph_format.first_line_indent = Cm(1.25)
    p.paragraph_format.line_spacing = 1.5
    p.paragraph_format.space_before = Pt(14)
    p.paragraph_format.space_after = Pt(6)
    p.paragraph_format.keep_with_next = True
    run = p.add_run(text)
    run.font.name = 'Times New Roman'
    run.font.size = Pt(14)
    run.bold = True
    return p

def add_heading_2(doc, text):
    """Заголовок второго уровня по СТП 01-2017"""
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p.paragraph_format.first_line_indent = Cm(1.25)
    p.paragraph_format.line_spacing = 1.5
    p.paragraph_format.space_before = Pt(10)
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.keep_with_next = True
    run = p.add_run(text)
    run.font.name = 'Times New Roman'
    run.font.size = Pt(14)
    run.bold = True
    return p

def add_paragraph_stp(doc, text, bold_prefix=None, italic_suffix=None):
    """Обычный абзац по СТП: отступ 1.25 см, выравнивание по ширине, полуторный интервал"""
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    p.paragraph_format.first_line_indent = Cm(1.25)
    p.paragraph_format.line_spacing = 1.5
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    
    if bold_prefix:
        r_pre = p.add_run(bold_prefix)
        r_pre.font.name = 'Times New Roman'
        r_pre.font.size = Pt(14)
        r_pre.bold = True
        
    r = p.add_run(text)
    r.font.name = 'Times New Roman'
    r.font.size = Pt(14)
    
    if italic_suffix:
        r_suf = p.add_run(italic_suffix)
        r_suf.font.name = 'Times New Roman'
        r_suf.font.size = Pt(14)
        r_suf.italic = True
        
    return p

def add_bullet_stp(doc, text, bold_prefix=None):
    """Элемент списка по СТП"""
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    p.paragraph_format.left_indent = Cm(1.75)
    p.paragraph_format.first_line_indent = Cm(-0.5)
    p.paragraph_format.line_spacing = 1.5
    p.paragraph_format.space_before = Pt(0)
    p.paragraph_format.space_after = Pt(0)
    
    r_bullet = p.add_run("– ")
    r_bullet.font.name = 'Times New Roman'
    r_bullet.font.size = Pt(14)
    
    if bold_prefix:
        r_pre = p.add_run(bold_prefix)
        r_pre.font.name = 'Times New Roman'
        r_pre.font.size = Pt(14)
        r_pre.bold = True
        
    r = p.add_run(text)
    r.font.name = 'Times New Roman'
    r.font.size = Pt(14)
    return p

def set_cell_borders(cell):
    """Устанавливает стандартные черные одинарные границы для ячейки таблицы"""
    tcPr = cell._tc.get_or_add_tcPr()
    tcBorders = parse_xml(
        r'<w:tcBorders %s>'
        r'  <w:top w:val="single" w:sz="4" w:space="0" w:color="000000"/>'
        r'  <w:left w:val="single" w:sz="4" w:space="0" w:color="000000"/>'
        r'  <w:bottom w:val="single" w:sz="4" w:space="0" w:color="000000"/>'
        r'  <w:right w:val="single" w:sz="4" w:space="0" w:color="000000"/>'
        r'</w:tcBorders>' % nsdecls('w')
    )
    tcPr.append(tcBorders)

def set_cell_shading(cell, color_hex="F2F2F2"):
    """Заливка фона ячейки"""
    tcPr = cell._tc.get_or_add_tcPr()
    shd = parse_xml(r'<w:shd %s w:fill="%s"/>' % (nsdecls('w'), color_hex))
    tcPr.append(shd)

def add_table_stp(doc, title, headers, rows_data, col_widths=None):
    """
    Добавляет таблицу по стандарту СТП 01-2017:
    - Подпись над таблицей: "Таблица X – Название таблицы" без точки в конце
    - Границы у всех ячеек
    - Повтор шапки таблицы на новой странице
    - Шрифт 10.5-11 pt, одинарный интервал
    """
    # Название таблицы
    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p_title.paragraph_format.first_line_indent = Cm(1.25)
    p_title.paragraph_format.line_spacing = 1.15
    p_title.paragraph_format.space_before = Pt(10)
    p_title.paragraph_format.space_after = Pt(4)
    p_title.paragraph_format.keep_with_next = True
    
    r_title = p_title.add_run(title)
    r_title.font.name = 'Times New Roman'
    r_title.font.size = Pt(13)
    r_title.bold = True
    
    num_cols = len(headers)
    num_rows = len(rows_data) + 1
    table = doc.add_table(rows=num_rows, cols=num_cols)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    
    # Шапка
    hdr_row = table.rows[0]
    hdr_trPr = hdr_row._tr.get_or_add_trPr()
    hdr_trPr.append(parse_xml(r'<w:tblHeader %s/>' % nsdecls('w')))
    hdr_trPr.append(parse_xml(r'<w:cantSplit %s/>' % nsdecls('w')))
    
    for c_idx, h_text in enumerate(headers):
        cell = hdr_row.cells[c_idx]
        set_cell_borders(cell)
        set_cell_shading(cell, "EAEAEA")
        cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.first_line_indent = Cm(0)
        p.paragraph_format.line_spacing = 1.0
        p.paragraph_format.space_before = Pt(4)
        p.paragraph_format.space_after = Pt(4)
        run = p.add_run(h_text)
        run.font.name = 'Times New Roman'
        run.font.size = Pt(11)
        run.bold = True
        
    # Данные
    for r_idx, row_data in enumerate(rows_data):
        row = table.rows[r_idx + 1]
        trPr = row._tr.get_or_add_trPr()
        trPr.append(parse_xml(r'<w:cantSplit %s/>' % nsdecls('w')))
        
        for c_idx, val in enumerate(row_data):
            cell = row.cells[c_idx]
            set_cell_borders(cell)
            cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
            p = cell.paragraphs[0]
            if c_idx == 0 and len(str(val)) <= 4:
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            else:
                p.alignment = WD_ALIGN_PARAGRAPH.LEFT
            p.paragraph_format.first_line_indent = Cm(0)
            p.paragraph_format.line_spacing = 1.05
            p.paragraph_format.space_before = Pt(3)
            p.paragraph_format.space_after = Pt(3)
            run = p.add_run(str(val))
            run.font.name = 'Times New Roman'
            run.font.size = Pt(10.5)
            
    # Применение ширин столбцов
    if col_widths:
        for r in table.rows:
            for c_idx, w in enumerate(col_widths):
                if c_idx < len(r.cells):
                    r.cells[c_idx].width = Cm(w)
                    
    # Отступ после таблицы
    p_after = doc.add_paragraph()
    p_after.paragraph_format.first_line_indent = Cm(0)
    p_after.paragraph_format.space_before = Pt(0)
    p_after.paragraph_format.space_after = Pt(6)
    
    return table
