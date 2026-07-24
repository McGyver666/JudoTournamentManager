import { Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';

interface SyncSample {
  rttMs: number;
  estimatedServerNowMs: number;
  responsePerfNowMs: number;
}

/**
 * Shared time source that estimates server UTC offset and advances locally via performance.now().
 */
@Injectable({ providedIn: 'root' })
export class TimeService {
  private readonly hasBaselineSignal = signal(false);
  private readonly staleSignal = signal(true);

  private baselineServerEpochMs = Date.now();
  private baselinePerfNowMs = performance.now();
  private lastSyncEpochMs = 0;
  private synchronizePromise: Promise<void> | null = null;

  constructor(private readonly api: ApiService) {}

  hasBaseline(): boolean {
    return this.hasBaselineSignal();
  }

  isStale(): boolean {
    return this.staleSignal();
  }

  nowMs(): number {
    if (!this.hasBaselineSignal()) {
      return Date.now();
    }

    const elapsedSinceBaselineMs = performance.now() - this.baselinePerfNowMs;
    return this.baselineServerEpochMs + elapsedSinceBaselineMs;
  }

  async synchronize(sampleCount = 5): Promise<void> {
    if (this.synchronizePromise) {
      await this.synchronizePromise;
      return;
    }

    this.synchronizePromise = this.synchronizeCore(sampleCount)
      .finally(() => {
        this.synchronizePromise = null;
      });

    await this.synchronizePromise;
  }

  async synchronizeIfStale(maxAgeMs = 5 * 60_000): Promise<void> {
    if (this.hasBaselineSignal() && Date.now() - this.lastSyncEpochMs < maxAgeMs) {
      return;
    }

    await this.synchronize(1);
  }

  ingestServerNowUtc(serverNowUtc: string): void {
    const parsed = Date.parse(serverNowUtc);
    if (Number.isNaN(parsed)) {
      return;
    }

    this.setBaseline(parsed, performance.now());
  }

  private async synchronizeCore(sampleCount: number): Promise<void> {
    const boundedSampleCount = Math.max(1, Math.min(5, sampleCount));
    let bestSample: SyncSample | null = null;

    for (let i = 0; i < boundedSampleCount; i++) {
      const requestStartPerf = performance.now();
      try {
        const response = await firstValueFrom(this.api.getServerTime());
        const responsePerf = performance.now();
        const serverEpochMs = Date.parse(response.serverTimeUtc);
        if (Number.isNaN(serverEpochMs)) {
          continue;
        }

        const rttMs = responsePerf - requestStartPerf;
        const estimatedServerAtResponseMs = serverEpochMs + (rttMs / 2);
        const sample: SyncSample = {
          rttMs,
          estimatedServerNowMs: estimatedServerAtResponseMs,
          responsePerfNowMs: responsePerf,
        };

        if (!bestSample || sample.rttMs < bestSample.rttMs) {
          bestSample = sample;
        }
      } catch {
        // Keep previous baseline and continue to next sample.
      }
    }

    if (!bestSample) {
      this.staleSignal.set(true);
      return;
    }

    this.setBaseline(bestSample.estimatedServerNowMs, bestSample.responsePerfNowMs);
  }

  private setBaseline(serverEpochMs: number, perfNowMs: number): void {
    this.baselineServerEpochMs = serverEpochMs;
    this.baselinePerfNowMs = perfNowMs;
    this.lastSyncEpochMs = Date.now();
    this.hasBaselineSignal.set(true);
    this.staleSignal.set(false);
  }
}
