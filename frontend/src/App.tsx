import { useState, useEffect, useCallback } from 'react';
import type { MonitoredNode, MetricUpdatePayload, IncidentAlertPayload, NodeRecoveredPayload } from './types';
import { useSignalR } from './hooks/useSignalR';
import { Header } from './components/Header';
import { NodeCard } from './components/NodeCard';
import { IncidentBanner } from './components/IncidentBanner';
import { ShieldCheck, Activity, Layers, Server } from 'lucide-react';

const INITIAL_NODES: MonitoredNode[] = [
  {
    id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
    name: 'Payment Gateway API',
    targetUrl: 'https://api.payments.internal/v1/charge',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 24,
  },
  {
    id: 'a12bc34d-58ee-4172-b567-1e02b2c3d480',
    name: 'Billing & Invoicing Service',
    targetUrl: 'https://billing.internal/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 38,
  },
  {
    id: 'c98de76f-12aa-4982-c567-2e02b2c3d481',
    name: 'Auth & Identity Cluster',
    targetUrl: 'https://auth.internal/oauth/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 19,
  },
];

export function App() {
  const [nodes, setNodes] = useState<MonitoredNode[]>(INITIAL_NODES);
  const [activeIncident, setActiveIncident] = useState<IncidentAlertPayload | null>(null);

  // 1. Попытка загрузить актуальные ноды из бэкенда
  useEffect(() => {
    fetch('http://localhost:5199/api/nodes')
      .then((res) => (res.ok ? res.json() : null))
      .then((data: MonitoredNode[] | null) => {
        if (data && data.length > 0) {
          setNodes(data);
        }
      })
      .catch(() => {
        // Если бэкенд выключен — используем мок-ноды
      });
  }, []);

  // 2. Обработка живой телеметрии из SignalR
  const handleMetricUpdated = useCallback((data: MetricUpdatePayload) => {
    setNodes((prev) =>
      prev.map((node) => {
        if (node.id === data.nodeId) {
          return {
            ...node,
            healthStatus: data.isSuccess ? 'Healthy' : 'Down',
            lastLatencyMs: data.responseTimeMs,
            lastCheckedAt: data.timestamp,
          };
        }
        return node;
      })
    );
  }, []);

  // 3. Обработка входящего инцидента с анализом от Gemini
  const handleIncidentAlert = useCallback((data: IncidentAlertPayload) => {
    setActiveIncident(data);
    setNodes((prev) =>
      prev.map((node) => {
        if (node.id === data.incident.nodeId) {
          return {
            ...node,
            healthStatus: 'Down',
            lastCheckedAt: data.timestamp,
          };
        }
        return node;
      })
    );
  }, []);

  // 4. Обработка восстановления ноды
  const handleNodeRecovered = useCallback((data: NodeRecoveredPayload) => {
    setActiveIncident((curr) => (curr?.incident.nodeId === data.nodeId ? null : curr));
    setNodes((prev) =>
      prev.map((node) => {
        if (node.id === data.nodeId) {
          return {
            ...node,
            healthStatus: 'Healthy',
            lastCheckedAt: data.timestamp,
          };
        }
        return node;
      })
    );
  }, []);

  // 5. Подключение хука SignalR
  const { isConnected } = useSignalR(handleMetricUpdated, handleIncidentAlert, handleNodeRecovered);

  // 6. Симуляция сбоя по клику на кнопку
  const handleSimulateFailure = async (nodeId: string) => {
    const targetNode = nodes.find((n) => n.id === nodeId);
    if (!targetNode) return;

    try {
      const res = await fetch(`http://localhost:5199/api/nodes/${nodeId}/simulate-failure`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          errorMessage: `HTTP 504 Gateway Timeout: Upstream dependency connection aborted after 30000ms`,
          failureType: 'HttpTimeout',
          statusCode: 504,
        }),
      });

      if (!res.ok) throw new Error('API request failed');
    } catch {
      // Graceful fallback для изолированного UI-демо
      handleIncidentAlert({
        incident: {
          incidentId: 'inc-' + Date.now(),
          nodeId: targetNode.id,
          nodeName: targetNode.name,
          targetUrl: targetNode.targetUrl,
          errorMessage: 'HTTP 504 Gateway Timeout: Upstream dependency connection aborted after 30000ms',
          failureType: 'HttpTimeout',
          statusCode: 504,
          occurredAt: new Date().toISOString(),
        },
        aiDiagnosis: {
          rootCause: `The ${targetNode.name} failed to complete its upstream database transaction within the 30-second timeout window due to thread-pool exhaustion or high connection latency.`,
          immediateFix: 'Restart or horizontally scale backend instances and cycle database connection pools.',
          prevention: 'Apply circuit breaker with Polly v8 and configure bulkheads to isolate slow upstream transactions.',
        },
        timestamp: new Date().toISOString(),
      });
    }
  };

  const handleResolveIncident = () => {
    if (!activeIncident) return;
    handleNodeRecovered({
      nodeId: activeIncident.incident.nodeId,
      nodeName: activeIncident.incident.nodeName,
      timestamp: new Date().toISOString(),
    });
  };

  const healthyNodesCount = nodes.filter((n) => n.healthStatus === 'Healthy').length;
  const healthPercent = Math.round((healthyNodesCount / nodes.length) * 100);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-indigo-500 selection:text-white">
      {/* Шапка дашборда */}
      <Header isConnected={isConnected} activeIncidentsCount={activeIncident ? 1 : 0} />

      {/* Основной контент */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Баннер активного инцидента с рекомендациями Gemini */}
        <IncidentBanner incidentAlert={activeIncident} onResolve={handleResolveIncident} />

        {/* Сводная панель метрик системы */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
          <div className="p-4 rounded-xl bg-slate-900/50 border border-slate-800">
            <div className="flex items-center gap-2 text-slate-400 text-xs mb-1">
              <Server className="w-4 h-4 text-indigo-400" />
              <span>Monitored Nodes</span>
            </div>
            <div className="text-2xl font-bold font-mono text-slate-100">{nodes.length}</div>
          </div>

          <div className="p-4 rounded-xl bg-slate-900/50 border border-slate-800">
            <div className="flex items-center gap-2 text-slate-400 text-xs mb-1">
              <Activity className="w-4 h-4 text-emerald-400" />
              <span>System Health</span>
            </div>
            <div
              className={`text-2xl font-bold font-mono ${
                healthPercent === 100 ? 'text-emerald-400' : 'text-amber-400'
              }`}
            >
              {healthPercent}%
            </div>
          </div>

          <div className="p-4 rounded-xl bg-slate-900/50 border border-slate-800">
            <div className="flex items-center gap-2 text-slate-400 text-xs mb-1">
              <Layers className="w-4 h-4 text-cyan-400" />
              <span>Resilience Engine</span>
            </div>
            <div className="text-sm font-semibold text-slate-200 mt-1">Polly v8 + Redis</div>
          </div>

          <div className="p-4 rounded-xl bg-slate-900/50 border border-slate-800">
            <div className="flex items-center gap-2 text-slate-400 text-xs mb-1">
              <ShieldCheck className="w-4 h-4 text-indigo-400" />
              <span>AI Diagnostics</span>
            </div>
            <div className="text-sm font-semibold text-slate-200 mt-1">Gemini 3.1 Flash</div>
          </div>
        </div>

        {/* Сетка карточек микросервисов */}
        <div className="mb-6 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold text-slate-100">Live Microservices Telemetry</h2>
            <p className="text-xs text-slate-400">
              Live WebSocket metrics stream updated every 5 seconds
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {nodes.map((node) => (
            <NodeCard key={node.id} node={node} onSimulateFailure={handleSimulateFailure} />
          ))}
        </div>
      </main>

      {/* Подвал */}
      <footer className="border-t border-slate-900 py-6 text-center text-xs text-slate-500">
        NexusPulse v2.0 • Distributed Observability Platform • Built with .NET 9, React 19 & Google Gemini AI
      </footer>
    </div>
  );
}

export default App;
