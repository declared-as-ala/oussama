import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, throwError } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ApiService } from '../../../core/services/api.service';
import { environment } from '../../../../environments/environment';
import {
  CreateDocumentRequest,
  DocumentActionLogResponse,
  CreateDocumentVersionRequest,
  DocumentDetailsResponse,
  DocumentExpiringResponse,
  DocumentQueryParams,
  DocumentResponse,
  DocumentStatisticsResponse,
  DocumentVersionResponse,
  PagedDocumentResponse,
  UpdateDocumentStatusRequest,
  UpdateDocumentRequest,
  UpdateDocumentVersionStatusRequest
} from '../models/document.models';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private readonly endpoint = 'documents';
  private readonly apiBase = `${environment.apiUrl}/api`;

  constructor(
    private readonly apiService: ApiService,
    private readonly http: HttpClient
  ) { }

  getDocuments(params: DocumentQueryParams = {}): Observable<PagedDocumentResponse> {
    return this.apiService.get<PagedDocumentResponse>(this.endpoint, params);
  }

  getTrash(params: DocumentQueryParams = {}): Observable<PagedDocumentResponse> {
    return this.apiService.get<PagedDocumentResponse>(`${this.endpoint}/trash`, params);
  }

  getDocumentById(id: number): Observable<DocumentDetailsResponse> {
    return this.apiService.get<DocumentDetailsResponse>(`${this.endpoint}/${id}`);
  }

  createDocument(payload: CreateDocumentRequest): Observable<DocumentResponse> {
    return this.apiService.post<DocumentResponse>(this.endpoint, payload);
  }

  updateDocument(id: number, payload: UpdateDocumentRequest): Observable<DocumentResponse> {
    return this.apiService.put<DocumentResponse>(`${this.endpoint}/${id}`, payload);
  }

  deleteDocument(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}`);
  }

  restoreDocument(id: number): Observable<void> {
    return this.apiService.patch<void>(`${this.endpoint}/${id}/restore`, {});
  }

  permanentDeleteDocument(id: number): Observable<void> {
    return this.apiService.delete<void>(`${this.endpoint}/${id}/permanent`);
  }

  toggleDocumentStatus(id: number): Observable<DocumentResponse> {
    return this.apiService.patch<DocumentResponse>(`${this.endpoint}/${id}/toggle-status`, {});
  }

  getDocumentStatistics(): Observable<DocumentStatisticsResponse> {
    return this.apiService.get<DocumentStatisticsResponse>(`${this.endpoint}/statistics`);
  }

  getExpiringDocuments(withinDays = 30): Observable<DocumentExpiringResponse[]> {
    return this.apiService.get<DocumentExpiringResponse[]>(`${this.endpoint}/expiring`, { withinDays });
  }

  updateDocumentStatus(id: number, payload: UpdateDocumentStatusRequest): Observable<DocumentResponse> {
    return this.apiService.put<DocumentResponse>(`${this.endpoint}/${id}/status`, payload);
  }

  getVersions(documentId: number): Observable<DocumentVersionResponse[]> {
    return this.apiService.get<DocumentVersionResponse[]>(`${this.endpoint}/${documentId}/versions`);
  }

  getActionLogs(documentId: number): Observable<DocumentActionLogResponse[]> {
    return this.apiService.get<DocumentActionLogResponse[]>(`${this.endpoint}/${documentId}/action-logs`);
  }

  getVersionById(documentId: number, versionId: number): Observable<DocumentVersionResponse> {
    return this.apiService.get<DocumentVersionResponse>(`${this.endpoint}/${documentId}/versions/${versionId}`);
  }

  createVersion(documentId: number, payload: CreateDocumentVersionRequest): Observable<DocumentVersionResponse> {
    return this.apiService.post<DocumentVersionResponse>(`${this.endpoint}/${documentId}/versions`, payload);
  }

  uploadVersion(documentId: number, file: File, metadata: CreateDocumentVersionRequest): Observable<DocumentVersionResponse> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('status', metadata.status);

    if (metadata.versionNumber) {
      formData.append('versionNumber', metadata.versionNumber);
    }

    if (metadata.revisionComment) {
      formData.append('revisionComment', metadata.revisionComment);
    }

    if (metadata.effectiveDate) {
      formData.append('effectiveDate', metadata.effectiveDate);
    }

    if (metadata.expiryDate) {
      formData.append('expiryDate', metadata.expiryDate);
    }

    if (metadata.signature) {
      formData.append('signature', metadata.signature);
    }

    return this.http.post<DocumentVersionResponse>(
      `${this.apiBase}/${this.endpoint}/${documentId}/upload`,
      formData
    );
  }

  updateVersionStatus(documentId: number, versionId: number, payload: UpdateDocumentVersionStatusRequest): Observable<DocumentVersionResponse> {
    return this.apiService.patch<DocumentVersionResponse>(`${this.endpoint}/${documentId}/versions/${versionId}/status`, payload);
  }

  downloadLatest(documentId: number, format?: string): Observable<{ blob: Blob; version: DocumentVersionResponse }> {
    return this.getLatestVersion(documentId).pipe(
      switchMap(latest => {
        if (!latest) {
          return throwError(() => ({ status: 404 }));
        }

        return this.downloadVersion(documentId, latest.id, format).pipe(
          map(blob => ({ blob, version: latest }))
        );
      })
    );
  }

  downloadCurrent(documentId: number, format?: string): Observable<Blob> {
    return this.downloadLatest(documentId, format).pipe(map(result => result.blob));
  }

  downloadVersion(documentId: number, versionId: number, format?: string): Observable<Blob> {
    const url = `${this.apiBase}/${this.endpoint}/${documentId}/versions/${versionId}/download` + (format ? `?format=${format}` : '');
    return this.http.get(url, {
      responseType: 'blob'
    });
  }

  previewCurrent(documentId: number): Observable<Blob> {
    return this.http.get(`${this.apiBase}/${this.endpoint}/${documentId}/preview-current`, {
      responseType: 'blob'
    });
  }

  previewCurrentOrLatest(documentId: number): Observable<Blob> {
    return this.getLatestVersion(documentId).pipe(
      switchMap(latest => {
        if (!latest) {
          return throwError(() => ({ status: 404 }));
        }

        return this.downloadVersion(documentId, latest.id);
      })
    );
  }

  private getLatestVersion(documentId: number): Observable<DocumentVersionResponse | null> {
    return this.getVersions(documentId).pipe(
      map(versions => {
        return [...versions].sort((a, b) => {
          const aTime = new Date(a.effectiveDate ?? a.createdAt).getTime();
          const bTime = new Date(b.effectiveDate ?? b.createdAt).getTime();
          return bTime - aTime;
        })[0] ?? null;
      })
    );
  }
}
