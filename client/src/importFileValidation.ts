export const maximumImportBytes = 5 * 1024 * 1024

export function validateImportFiles(files: readonly Pick<File, 'name' | 'size'>[]): string {
  if (files.length === 0) return 'Excel faylını buraxın. Qovluq və ya link qəbul edilmir.'
  if (files.length !== 1) return 'Hər dəfə yalnız bir Excel faylı seçin.'
  if (!/\.xlsx$/i.test(files[0].name)) return 'Yalnız .xlsx faylı seçin.'
  if (files[0].size === 0) return 'Seçilmiş fayl boşdur.'
  if (files[0].size > maximumImportBytes) return 'Faylın ölçüsü 5 MB-dan böyük ola bilməz.'
  return ''
}
