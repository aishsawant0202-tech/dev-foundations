import PyPDF2

template = PyPDF2.PdfReader(open('combined.pdf', 'rb'))
watermark = PyPDF2.PdfReader(open('watermark.pdf', 'rb'))
output = PyPDF2.PdfWriter()

for i in range(len(template.pages)):
    page = template.pages[i]
    page.merge_page(watermark.pages[0])
    output.add_page(page)

with open('watermarked.pdf', 'wb') as file:
    output.write(file)

# import sys

# inputs = sys.argv[1:]


# def pdf_combiner(pdf_list):
#     merger = PyPDF2.PdfMerger()
#     for pdf in pdf_list:
#         print(pdf)
#         merger.append(pdf)
#     merger.write('combined.pdf')


# pdf_combiner(inputs)


# with open('DummyFile.pdf', 'rb') as file:
#     reader = PyPDF2.PdfReader(file)
#     print(len(reader.pages))
#     print(reader.pages[0].extract_text())
#     print(reader.pdf_header)
#     print(reader.pages[0].rotate(90))
#     with open('tilt.pdf', 'wb') as file2:
#         writer = PyPDF2.PdfWriter()
#         writer.add_page(reader.pages[0])
#         writer.write(file2)
