import { canAutoRotateHero, nextHeroIndex } from './overview-hero-state';

describe('overview hero rotation', () => {
  it('wraps manually in both directions', () => {
    expect(nextHeroIndex(0, 3, 1)).toBe(1);
    expect(nextHeroIndex(0, 3, -1)).toBe(2);
    expect(nextHeroIndex(2, 3, 1)).toBe(0);
  });

  it('does not auto-rotate when motion or focus safety requires a pause', () => {
    expect(canAutoRotateHero({ reducedMotion: true, interactionPaused: false, osaeKomiActive: false })).toBeFalse();
    expect(canAutoRotateHero({ reducedMotion: false, interactionPaused: true, osaeKomiActive: false })).toBeFalse();
    expect(canAutoRotateHero({ reducedMotion: false, interactionPaused: false, osaeKomiActive: true })).toBeFalse();
  });

  it('auto-rotates when the hero is idle and motion is allowed', () => {
    expect(canAutoRotateHero({ reducedMotion: false, interactionPaused: false, osaeKomiActive: false })).toBeTrue();
  });
});
