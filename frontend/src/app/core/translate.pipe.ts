import { Pipe, PipeTransform } from '@angular/core';
import { I18nService } from './i18n.service';

/**
 * Impure pipe that resolves a translation key against the active language.
 * Marked impure so that switching languages re-renders bound strings.
 *
 * Usage: <c>{{ 'tournaments.title' | t }}</c> or
 * <c>{{ 'errors.duplicate' | t: { name: club.name } }}</c>.
 */
@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  constructor(private readonly i18n: I18nService) {}

  transform(key: string, params?: Record<string, string | number>): string {
    return this.i18n.translate(key, params);
  }
}
