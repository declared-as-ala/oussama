import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly baseUrl = `${environment.apiUrl}/api`;

  constructor(private readonly http: HttpClient) {}

  get<T>(endpoint: string, params?: object, headers?: HttpHeaders | Record<string, string>): Observable<T> {
    return this.http.get<T>(this.buildUrl(endpoint), {
      params: this.buildHttpParams(params),
      headers: this.buildHeaders(headers)
    });
  }

  post<T>(endpoint: string, data: unknown, headers?: HttpHeaders | Record<string, string>): Observable<T> {
    return this.http.post<T>(this.buildUrl(endpoint), data, {
      headers: this.buildHeaders(headers)
    });
  }

  put<T>(endpoint: string, data: unknown, headers?: HttpHeaders | Record<string, string>): Observable<T> {
    return this.http.put<T>(this.buildUrl(endpoint), data, {
      headers: this.buildHeaders(headers)
    });
  }

  patch<T>(endpoint: string, data: unknown, headers?: HttpHeaders | Record<string, string>): Observable<T> {
    return this.http.patch<T>(this.buildUrl(endpoint), data, {
      headers: this.buildHeaders(headers)
    });
  }

  delete<T>(endpoint: string, headers?: HttpHeaders | Record<string, string>): Observable<T> {
    return this.http.delete<T>(this.buildUrl(endpoint), {
      headers: this.buildHeaders(headers)
    });
  }

  private buildUrl(endpoint: string): string {
    const normalizedEndpoint = endpoint.replace(/^\/+/, '');
    return `${this.baseUrl}/${normalizedEndpoint}`;
  }

  private buildHttpParams(params?: object): HttpParams {
    let httpParams = new HttpParams();
    if (!params) return httpParams;
    Object.entries(params as Record<string, unknown>).forEach(([key, value]) => {
      if (value !== null && value !== undefined) {
        httpParams = httpParams.set(key, String(value));
      }
    });
    return httpParams;
  }

  private buildHeaders(headers?: HttpHeaders | Record<string, string>): HttpHeaders {
    if (headers instanceof HttpHeaders) {
      return headers;
    }
    let httpHeaders = new HttpHeaders();
    if (headers) {
      Object.entries(headers).forEach(([key, value]) => {
        httpHeaders = httpHeaders.set(key, value);
      });
    }
    return httpHeaders;
  }
}
