import { InjectionToken } from '@angular/core';

/** Base path of the API. Same origin in every environment (dev proxy, single container in production). */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => '/api/v1',
});
