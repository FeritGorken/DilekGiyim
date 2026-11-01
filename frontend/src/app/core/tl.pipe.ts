import { Pipe, PipeTransform } from '@angular/core';

/** 3490 → "3.490,00 TL" */
export function formatTl(value: number | null | undefined): string {
  if (value === null || value === undefined || isNaN(value)) return '';
  return value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' TL';
}

@Pipe({ name: 'tl' })
export class TlPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return formatTl(value);
  }
}
