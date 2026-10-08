import { Component, computed, input, ChangeDetectionStrategy } from '@angular/core';
import { TranslatePipe } from '../../core/translate.pipe';

/** Shido count that results in Hansoku-make (mirrors the backend's MatchService.HansokuMakeShidoCount). */
export const HANSOKU_MAKE_SHIDO_COUNT = 3;

/**
 * Shows a fighter's shidos as bubbles, or a "Hansoku-make" badge once the
 * Hansoku-make threshold is reached.
 */
@Component({
  selector: 'app-shido-indicator',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './shido-indicator.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './shido-indicator.component.css',
})
export class ShidoIndicatorComponent {
  /** Number of shidos the fighter has received. */
  readonly count = input.required<number>();
  /** `xl` is the high-visibility variant used on the tatami hall display. */
  readonly size = input<'normal' | 'xl'>('normal');
  /** CSS class marking a filled bubble; differs between the operator and display stylesheets. */
  readonly activeClass = input<'shido-bubble--active' | 'active'>('shido-bubble--active');

  protected readonly slots = Array.from({ length: HANSOKU_MAKE_SHIDO_COUNT }, (_, i) => i);
  protected readonly isHansokuMake = computed(() => this.count() >= HANSOKU_MAKE_SHIDO_COUNT);
  protected readonly xl = computed(() => this.size() === 'xl');
}
