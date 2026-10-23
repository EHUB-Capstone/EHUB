const cvFormLabels: RegExp[] = [
  /Thời gian(?:\s*\([^)]*\))?/gi,
  /Tên trường\/cơ sở đào tạo, quốc gia/gi,
  /Chuyên ngành đào tạo/gi,
  /Loại hình đào tạo/gi,
  /Các ngoại ngữ có thể sử dụng/gi,
  /Trình độ sử dụng\s*\(5-Xuất sắc;\s*4-Tốt;\s*3-Khá;\s*2-Trung bình;\s*1-Kém\)/gi,
  /Nghe\s+Nói\s+Đọc\s+Viết/gi,
  /Xếp loại\s*\/\s*Điểm/gi,
  /Xếp loại/gi,
  /Bằng\s*\/\s*\(nếu có\)/gi,
  /Ngày cấp/gi,
  /\(nếu có\)/gi,
];

const cvFormLabelOnly = /^(?:thời gian|tên trường\/cơ sở đào tạo, quốc gia|chuyên ngành đào tạo|loại hình đào tạo|xếp loại(?:\s*\/\s*(?:điểm)?)?|điểm|các ngoại ngữ có thể sử dụng|bằng\s*\/?(?:\s*\(nếu có\))?|ngày cấp|trình độ sử dụng|nghe|nói|đọc|viết)$/i;

export function isMentorCvFormLabel(value: string): boolean {
  return cvFormLabelOnly.test(value.trim());
}

export interface ParsedMentorCvBio {
  education: Array<{ period: string; institution: string; details: string[] }>;
  thesis: string[];
  languagesAndCertificates: string[];
}

const dateRange = /^(?:(?:0?[1-9]|1[0-2])\/)?(?:19|20)\d{2}\s*[-–—]\s*(?:(?:0?[1-9]|1[0-2])\/)?(?:19|20)\d{2}|^(?:(?:0?[1-9]|1[0-2])\/)?(?:19|20)\d{2}\s*[-–—]\s*(?:nay|hiện tại|present)$/i;
const vietnameseFormNoise = /^(?:\(?từ tháng\/năm\)?|\(?đến tháng\/năm\)?|các ngoại ngữ có thể sử dụng|bằng\s*\/?|\(nếu có\)|xếp loại\s*\/?\s*điểm|ngày cấp|trình độ sử dụng.*|nghe|nói|đọc|viết)$/i;

function cleanMentorFormLine(value: string): string {
  const line = value.trim().replace(/^\*\s*/, '');
  if (isMentorCvFormLabel(line) || vietnameseFormNoise.test(line)) return '';
  return line;
}

/** Removes blank work-history form prompts while keeping information entered by the mentor. */
export function parseMentorProfessionalExperience(value: string | null | undefined): string[] {
  return (value ?? '')
    .split(/\r?\n/)
    .map(line => line.trim()
      .replace(/thời gian/gi, '')
      .replace(/\(?từ tháng\/năm/gi, '')
      .replace(/đến tháng\/năm\)?/gi, '')
      .replace(/[()]/g, '')
      .replace(/\s{2,}/g, ' ')
      .trim())
    .filter(line => line && !/^(?:professional experience|kinh nghiệm làm việc)$/i.test(line));
}

/** Structures the education table in the standard mentor form without altering saved CV data. */
export function parseMentorCvFormBio(value: string | null | undefined): ParsedMentorCvBio | null {
  const lines = (value ?? '').split(/\r?\n/).map(line => line.trim()).filter(Boolean);
  const firstDate = lines.findIndex(line => dateRange.test(line));
  const hasFormLabels = lines.some(line => /^thời gian$/i.test(line)) &&
    lines.some(line => /^tên trường\/cơ sở đào tạo, quốc gia$/i.test(line));
  if (!hasFormLabels || firstDate < 0) return null;

  const languageHeading = lines.findIndex((line, index) => index > firstDate && /^(?:các ngoại ngữ có thể sử dụng|ngoại ngữ)/i.test(line));
  const educationEnd = languageHeading < 0 ? lines.length : languageHeading;
  const thesis = lines.slice(firstDate, educationEnd)
    .map(line => line.match(/^\*?\s*Tên chuyên đề luận án tốt nghiệp bậc cao nhất\s*:\s*(.+)$/i)?.[1]?.trim())
    .filter((line): line is string => Boolean(line));
  const educationLines = lines.slice(firstDate, educationEnd)
    .filter(line => !/^\*?\s*Tên chuyên đề luận án tốt nghiệp bậc cao nhất\s*:/i.test(line))
    .map(cleanMentorFormLine).filter(Boolean);
  const entries: ParsedMentorCvBio['education'] = [];
  for (const line of educationLines) {
    if (dateRange.test(line)) entries.push({ period: line.replace(/\s*[-–—]\s*/, ' – '), institution: '', details: [] });
    else if (entries.length > 0) {
      const entry = entries[entries.length - 1];
      if (!entry.institution) entry.institution = line;
      else entry.details.push(line);
    }
  }

  const languagesAndCertificates = (languageHeading < 0 ? [] : lines.slice(languageHeading + 1))
    .map(cleanMentorFormLine).filter(Boolean);
  return { education: entries.filter(entry => entry.institution), thesis, languagesAndCertificates };
}

/** Removes field prompts from the sample mentor form when older imports stored them in Bio. */
export function formatMentorDirectoryBio(value: string | null | undefined): string {
  let formatted = value?.trim() ?? '';
  for (const label of cvFormLabels) formatted = formatted.replace(label, ' ');
  return formatted.replace(/[ \t]+/g, ' ').replace(/\s*\n\s*/g, '\n').replace(/\s{2,}/g, ' ').trim();
}
