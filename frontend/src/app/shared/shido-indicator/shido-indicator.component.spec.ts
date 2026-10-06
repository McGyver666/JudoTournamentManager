import { TestBed } from '@angular/core/testing';
import { I18nService } from '../../core/i18n.service';
import { HANSOKU_MAKE_SHIDO_COUNT, ShidoIndicatorComponent } from './shido-indicator.component';

describe('ShidoIndicatorComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ShidoIndicatorComponent],
      providers: [{ provide: I18nService, useValue: { translate: (key: string) => key } }],
    });
  });

  function render(inputs: { count: number; size?: 'normal' | 'xl'; activeClass?: 'shido-bubble--active' | 'active' }): HTMLElement {
    const fixture = TestBed.createComponent(ShidoIndicatorComponent);
    fixture.componentRef.setInput('count', inputs.count);
    if (inputs.size) fixture.componentRef.setInput('size', inputs.size);
    if (inputs.activeClass) fixture.componentRef.setInput('activeClass', inputs.activeClass);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders one bubble per shido slot and marks the received shidos', () => {
    const root = render({ count: 2 });

    const bubbles = root.querySelectorAll('.shido-bubble');
    expect(bubbles.length).toBe(HANSOKU_MAKE_SHIDO_COUNT);
    expect(root.querySelectorAll('.shido-bubble--active').length).toBe(2);
    expect(root.querySelector('.active')).toBeNull();
    expect(root.querySelector('.hansoku-make-badge')).toBeNull();
    expect(root.querySelector('.shido-bubbles')?.getAttribute('aria-label')).toBe('match.shido');
  });

  it('uses the configured active class', () => {
    const root = render({ count: 1, activeClass: 'active' });

    expect(root.querySelectorAll('.shido-bubble.active').length).toBe(1);
    expect(root.querySelector('.shido-bubble--active')).toBeNull();
  });

  it('replaces the bubbles with a labelled Hansoku-make badge at the threshold', () => {
    const root = render({ count: HANSOKU_MAKE_SHIDO_COUNT });

    const badge = root.querySelector('.hansoku-make-badge');
    expect(badge?.textContent).toContain('match.hansokuMake');
    expect(badge?.getAttribute('aria-label')).toBe('match.shido: match.hansokuMake');
    expect(root.querySelectorAll('.shido-bubble').length).toBe(0);
  });

  it('applies the XL classes only for the xl size', () => {
    expect(render({ count: 0, size: 'xl' }).querySelectorAll('.shido-bubble--xl').length).toBe(HANSOKU_MAKE_SHIDO_COUNT);
    expect(render({ count: HANSOKU_MAKE_SHIDO_COUNT, size: 'xl' }).querySelector('.hansoku-make-badge--xl')).not.toBeNull();
    expect(render({ count: 0 }).querySelector('.shido-bubble--xl')).toBeNull();
  });
});
