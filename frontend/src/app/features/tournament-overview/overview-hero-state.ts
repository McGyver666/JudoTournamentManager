export interface HeroRotationOptions {
  reducedMotion: boolean;
  interactionPaused: boolean;
  osaeKomiActive: boolean;
}

export function canAutoRotateHero(options: HeroRotationOptions): boolean {
  return !options.reducedMotion && !options.interactionPaused && !options.osaeKomiActive;
}

export function nextHeroIndex(currentIndex: number, itemCount: number, direction: 1 | -1): number {
  if (itemCount <= 0) {
    return 0;
  }

  return (currentIndex + direction + itemCount) % itemCount;
}
