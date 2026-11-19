export type NodeHealthStatus = 'Healthy' | 'Degraded' | 'Down';

export interface MonitoredNode {
  id: string;
  name: string;
  targetUrl: string;
  checkIntervalSeconds: number;
  timeoutSeconds: number;
  healthStatus: NodeHealthStatus;
  lastCheckedAt: string | null;
  isActive: boolean;
  lastLatencyMs?: number;
}

export interface AiDiagnosis {
  rootCause: string;
  immediateFix: string;
  prevention: string;
}

export interface Incident {
  id: string;
  nodeId: string;
  severity: 'Warning' | 'Critical';
  errorMessage: string;
  failureType: string;
  aiRootCause: string | null;
  aiImmediateFix: string | null;
  aiLongTermPrevention: string | null;
  status: 'Active' | 'Resolved';
  startedAt: string;
  resolvedAt: string | null;
}

export interface MetricUpdatePayload {
  nodeId: string;
  responseTimeMs: number;
  statusCode: number;
  isSuccess: boolean;
  timestamp: string;
}

export interface IncidentAlertPayload {
  incident: {
    incidentId: string;
    nodeId: string;
    nodeName: string;
    targetUrl: string;
    errorMessage: string;
    failureType: string;
    statusCode: number | null;
    occurredAt: string;
  };
  aiDiagnosis: AiDiagnosis | null;
  timestamp: string;
}

export interface NodeRecoveredPayload {
  nodeId: string;
  nodeName: string;
  timestamp: string;
}
