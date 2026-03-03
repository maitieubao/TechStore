import sys
from pypdf import PdfReader

def extract_text(pdf_path, out_path):
    with open(out_path, "w", encoding="utf-8") as f:
        reader = PdfReader(pdf_path)
        for i, page in enumerate(reader.pages):
            f.write(f"--- PAGE {i+1} ---\n")
            f.write(page.extract_text() + "\n")

if __name__ == '__main__':
    extract_text(sys.argv[1], sys.argv[2])
