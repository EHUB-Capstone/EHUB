"""Renders the synthetic submissions in fixtures.py to PDF/DOCX/PPTX files plus a manifest."""
import argparse
import json
from pathlib import Path

from docx import Document
from fpdf import FPDF
from pptx import Presentation
from pptx.util import Pt

from fixtures import CHECKPOINTS, SUBMISSIONS


def write_pdf(path, sections, font):
    pdf = FPDF()
    pdf.add_font("Body", "", font)
    pdf.add_page()
    for heading, paragraphs in sections:
        pdf.set_font("Body", size=14)
        pdf.multi_cell(0, 8, heading, new_x="LMARGIN", new_y="NEXT")
        pdf.set_font("Body", size=11)
        for paragraph in paragraphs:
            pdf.multi_cell(0, 6, paragraph, new_x="LMARGIN", new_y="NEXT")
            pdf.ln(1)
        pdf.ln(3)
    pdf.output(str(path))


def write_docx(path, sections):
    document = Document()
    for heading, paragraphs in sections:
        document.add_heading(heading, level=2)
        for paragraph in paragraphs:
            document.add_paragraph(paragraph)
    document.save(str(path))


def write_pptx(path, sections):
    deck = Presentation()
    for heading, bullets in sections:
        slide = deck.slides.add_slide(deck.slide_layouts[1])
        slide.shapes.title.text = heading
        body = slide.placeholders[1].text_frame
        body.text = bullets[0]
        for bullet in bullets[1:]:
            body.add_paragraph().text = bullet
        for paragraph in body.paragraphs:
            for run in paragraph.runs:
                run.font.size = Pt(18)
    deck.save(str(path))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="artifacts/checkpoint-summary/fixtures")
    parser.add_argument("--font", default="C:/Windows/Fonts/arial.ttf",
                        help="A TTF font with Vietnamese glyphs, used for PDF output.")
    args = parser.parse_args()
    root = Path(args.output)
    manifest = []
    for item in SUBMISSIONS:
        folder = root / item["id"]
        folder.mkdir(parents=True, exist_ok=True)
        names = []
        for file in item["files"]:
            path = folder / file["name"]
            if file["kind"] == "pdf": write_pdf(path, file["sections"], args.font)
            elif file["kind"] == "docx": write_docx(path, file["sections"])
            else: write_pptx(path, file["sections"])
            names.append(file["name"])
        manifest.append({"id": item["id"], "title": item["title"], "checkpoint": item["checkpoint"],
                         "requirements": CHECKPOINTS[item["checkpoint"]], "files": names,
                         "links": item["links"], "truth": item["truth"], "facts": item["facts"],
                         "injection": item.get("injection", []),
                         "figure_conflicts": item.get("figure_conflicts", [])})
    (root / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {len(manifest)} submissions to {root}")


if __name__ == "__main__":
    main()
