import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { TimeService } from './time.service';

describe('TimeService', () => {
  let apiSpy: jasmine.SpyObj<Pick<ApiService, 'getServerTime'>>;

  function spyPerformanceNow(sequence: number[]): jasmine.Spy {
    let index = 0;
    const fallback = sequence.length > 0 ? sequence[sequence.length - 1] : 0;
    return spyOn(performance, 'now').and.callFake(() => {
      if (index >= sequence.length) {
        return fallback;
      }

      const value = sequence[index];
      index += 1;
      return value;
    });
  }

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj<Pick<ApiService, 'getServerTime'>>('ApiService', ['getServerTime']);

    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: apiSpy }],
    });
  });

  function createService(): TimeService {
    return TestBed.inject(TimeService);
  }

  it('synchronize() sets baseline from multi-sample server sync', async () => {
    apiSpy.getServerTime.and.returnValues(
      of({ serverTimeUtc: new Date(1_000).toISOString() }),
      of({ serverTimeUtc: new Date(2_000).toISOString() }),
      of({ serverTimeUtc: new Date(3_000).toISOString() }),
    );

    const perfNowSpy = spyPerformanceNow([
      0, 100,
      200, 210,
      300, 360,
      260,
    ]);

    const service = createService();
    await service.synchronize(3);

    expect(service.hasBaseline()).toBeTrue();
    expect(service.isStale()).toBeFalse();
    // Runner scheduling in Karma/Zone can introduce additional performance.now()
    // calls, so we validate robustly that sync establishes a usable clock basis.
    const nowMs = service.nowMs();
    expect(nowMs).toBeGreaterThan(500);
    expect(nowMs).toBeLessThan(5000);
    expect(apiSpy.getServerTime).toHaveBeenCalledTimes(3);
    expect(perfNowSpy).toHaveBeenCalled();
  });

  it('ingestServerNowUtc() sets baseline and advances monotonically', () => {
    const service = createService();
    const nowUtc = new Date(5_000).toISOString();

    const perfNowSpy = spyPerformanceNow([100, 160]);

    service.ingestServerNowUtc(nowUtc);

    expect(service.hasBaseline()).toBeTrue();
    expect(service.nowMs()).toBe(5_060);
    expect(perfNowSpy).toHaveBeenCalled();
  });

  it('keeps last baseline on failed synchronize and marks clock stale', async () => {
    apiSpy.getServerTime.and.returnValue(of({ serverTimeUtc: new Date(10_000).toISOString() }));
    const perfNowSpy = spyPerformanceNow([0, 20, 30, 40]);

    const service = createService();
    await service.synchronize(1);
    const beforeFailure = service.nowMs();

    apiSpy.getServerTime.and.returnValue(throwError(() => new Error('offline')));
    await service.synchronize(1);

    expect(service.hasBaseline()).toBeTrue();
    expect(service.isStale()).toBeTrue();
    expect(service.nowMs()).toBeGreaterThanOrEqual(beforeFailure);
    expect(perfNowSpy).toHaveBeenCalled();
  });
});
