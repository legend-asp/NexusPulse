import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import type { MonitoredNode, MetricUpdatePayload, IncidentAlertPayload, NodeRecoveredPayload } from './types';
import { useSignalR } from './hooks/useSignalR';
import { Header } from './components/Header';
import { NodeCard } from './components/NodeCard';
import { IncidentBanner } from './components/IncidentBanner';
import { IncidentModal } from './components/IncidentModal';
import { Activity, Layers, Server, Cpu } from 'lucide-react';

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

const getNodeDiagnosis = (node: MonitoredNode) => {
  if (node.name.includes('Payment')) {
    return {
      rootCause: 'Upstream banking acquiring gateway timed out after 30000ms during credit card tokenization due to acquiring bank thread-pool exhaustion.',
      immediateFix: 'Trip Polly v8 Circuit Breaker to OPEN, failover to secondary merchant gateway, and drain socket queue.',
      prevention: 'Configure Keyed Polly v8 resilience pipeline with Jitter Exponential Retry and isolated bulkhead pools.'
    };
  }
  if (node.name.includes('Billing')) {
    return {
      rootCause: 'PostgreSQL transaction deadlock detected during recurring subscription invoice generation under heavy row lock contention.',
      immediateFix: 'Terminate blocking backend transaction PID, cycle read-replica connection pools, and restart worker pods.',
      prevention: 'Enforce strict statement_timeout (3s) on billing queries and migrate bulk invoice aggregation to asynchronous background jobs.'
    };
  }
  return {
    rootCause: 'OAuth2 / JWT token validation cluster exhausted available HTTP connection sockets due to sudden authentication spike.',
    immediateFix: 'Increase SocketsHttpHandler connection pool limit, restart auth pods, and enable distributed Redis token caching.',
    prevention: 'Deploy rate limiting at edge reverse proxy and configure auto-scaling group with min 3 replicas.'
  };
};

export function App() {
  const [nodes, setNodes] = useState<MonitoredNode[]>(INITIAL_NODES);
  const [incidents, setIncidents] = useState<Record<string, IncidentAlertPayload>>({});
  const [modalNode, setModalNode] = useState<MonitoredNode | null>(null);

  // Реф для предотвращения случайного сброса упавшей ноды фоновым SignalR-пульсом
  const incidentsRef = useRef(incidents);
  useEffect(() => {
    incidentsRef.current = incidents;
  }, [incidents]);

  // 1. Загрузка нод из бэкенда при монтировании
  useEffect(() => {
    fetch('http://localhost:5199/api/nodes')
      .then((res) => (res.ok ? res.json() : null))
      .then((data: MonitoredNode[] | null) => {
        if (data && data.length > 0) {
          setNodes(() =>
            data.map((serverNode) => {
              const activeIncident = incidentsRef.current[serverNode.id.toLowerCase()];
              return {
                ...serverNode,
                healthStatus: activeIncident ? 'Down' : serverNode.healthStatus,
              };
            })
          );
        }
      })
      .catch(() => {
        // Fallback: мок-ноды
      });
  }, []);

  // 2. Обработка живой телеметрии из SignalR
  const handleMetricUpdated = useCallback((data: MetricUpdatePayload) => {
    const rawData = data as any;
    const nodeId = (rawData.nodeId || rawData.NodeId || '').toLowerCase();
    if (!nodeId) return;

    setNodes((prev) =>
      prev.map((node) => {
        if (node.id.toLowerCase() === nodeId) {
          const hasActiveIncident = !!incidentsRef.current[node.id.toLowerCase()];
          const isSuccess = rawData.isSuccess ?? rawData.IsSuccess ?? true;
          return {
            ...node,
            healthStatus: hasActiveIncident ? 'Down' : isSuccess ? 'Healthy' : 'Down',
            lastLatencyMs: rawData.responseTimeMs ?? rawData.ResponseTimeMs ?? node.lastLatencyMs,
            lastCheckedAt: rawData.timestamp ?? rawData.Timestamp ?? new Date().toISOString(),
          };
        }
        return node;
      })
    );
  }, []);

  // 3. Обработка входящего инцидента с анализом
  const handleIncidentAlert = useCallback((data: IncidentAlertPayload) => {
    const raw = data as any;
    const rawIncident = raw.incident || raw.Incident || {};
    const nodeId = (rawIncident.nodeId || rawIncident.NodeId || '').toLowerCase();
    if (!nodeId) return;

    const normalizedPayload: IncidentAlertPayload = {
      incident: {
        incidentId: rawIncident.incidentId || rawIncident.IncidentId || 'inc-' + Date.now(),
        nodeId: nodeId,
        nodeName: rawIncident.nodeName || rawIncident.NodeName || 'Microservice',
        targetUrl: rawIncident.targetUrl || rawIncident.TargetUrl || '',
        errorMessage: rawIncident.errorMessage || rawIncident.ErrorMessage || 'HTTP 504 Gateway Timeout',
        failureType: rawIncident.failureType || rawIncident.FailureType || 'HttpTimeout',
        statusCode: rawIncident.statusCode ?? rawIncident.StatusCode ?? 504,
        occurredAt: rawIncident.occurredAt || rawIncident.OccurredAt || new Date().toISOString(),
      },
      aiDiagnosis: raw.aiDiagnosis || raw.AiDiagnosis || null,
      timestamp: raw.timestamp || raw.Timestamp || new Date().toISOString(),
    };

    setIncidents((prev) => ({
      ...prev,
      [nodeId]: normalizedPayload,
    }));

    setNodes((prev) =>
      prev.map((node) => {
        if (node.id.toLowerCase() === nodeId) {
          return {
            ...node,
            healthStatus: 'Down',
            lastCheckedAt: normalizedPayload.timestamp,
          };
        }
        return node;
      })
    );
  }, []);

  // 4. Обработка восстановления ноды
  const handleNodeRecovered = useCallback((data: NodeRecoveredPayload) => {
    const raw = data as any;
    const nodeId = (raw.nodeId || raw.NodeId || '').toLowerCase();
    if (!nodeId) return;

    setIncidents((prev) => {
      const next = { ...prev };
      delete next[nodeId];
      return next;
    });

    setNodes((prev) =>
      prev.map((node) => {
        if (node.id.toLowerCase() === nodeId) {
          return {
            ...node,
            healthStatus: 'Healthy',
            lastLatencyMs: Math.floor(Math.random() * 20) + 15,
            lastCheckedAt: new Date().toISOString(),
          };
        }
        return node;
      })
    );

    setModalNode((curr) => (curr?.id.toLowerCase() === nodeId ? null : curr));
  }, []);

  // 5. SignalR хук
  const { isConnected } = useSignalR(handleMetricUpdated, handleIncidentAlert, handleNodeRecovered);

  // 6. МГНОВЕННАЯ симуляция сбоя с оптимистичным обновлением
  const handleSimulateFailure = async (nodeId: string) => {
    const targetNode = nodes.find((n) => n.id.toLowerCase() === nodeId.toLowerCase());
    if (!targetNode) return;

    const lowerId = targetNode.id.toLowerCase();

    // 1. Мгновенно переключаем состояние ноды в UI (0 задержки)
    const initialIncident: IncidentAlertPayload = {
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
      aiDiagnosis: getNodeDiagnosis(targetNode),
      timestamp: new Date().toISOString(),
    };

    setIncidents((prev) => ({
      ...prev,
      [lowerId]: initialIncident,
    }));

    setNodes((prev) =>
      prev.map((n) =>
        n.id.toLowerCase() === lowerId
          ? { ...n, healthStatus: 'Down', lastLatencyMs: 0, lastCheckedAt: new Date().toISOString() }
          : n
      )
    );

    // 2. Отправляем запрос в бэкенд для Telegram-алерта и Gemini
    try {
      await fetch(`http://localhost:5199/api/nodes/${targetNode.id}/simulate-failure`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          errorMessage: 'HTTP 504 Gateway Timeout: Upstream dependency connection aborted after 30000ms',
          failureType: 'HttpTimeout',
          statusCode: 504,
        }),
      });
    } catch { }
  };

  // 7. Мгновенное восстановление конкретной ноды
  const handleRestoreNode = async (nodeId: string) => {
    const lowerId = nodeId.toLowerCase();

    // Оптимистичное обновление UI
    setNodes((prev) =>
      prev.map((node) => {
        if (node.id.toLowerCase() === lowerId) {
          return {
            ...node,
            healthStatus: 'Healthy',
            lastLatencyMs: Math.floor(Math.random() * 20) + 15,
            lastCheckedAt: new Date().toISOString(),
          };
        }
        return node;
      })
    );

    setIncidents((prev) => {
      const next = { ...prev };
      delete next[lowerId];
      return next;
    });

    if (modalNode?.id.toLowerCase() === lowerId) {
      setModalNode(null);
    }

    // Оповещаем бэкенд для Telegram-уведомления
    try {
      await fetch(`http://localhost:5199/api/nodes/${nodeId}/restore`, { method: 'POST' });
    } catch { }
  };

  // 8. Восстановление всех упавших нод
  const handleResolveAll = async () => {
    const downNodes = nodes.filter((n) => n.healthStatus === 'Down');

    setNodes((prev) =>
      prev.map((n) => ({
        ...n,
        healthStatus: 'Healthy',
        lastLatencyMs: n.healthStatus === 'Down' ? Math.floor(Math.random() * 20) + 15 : n.lastLatencyMs,
        lastCheckedAt: new Date().toISOString(),
      }))
    );
    setIncidents({});
    setModalNode(null);

    for (const node of downNodes) {
      try {
        await fetch(`http://localhost:5199/api/nodes/${node.id}/restore`, { method: 'POST' });
      } catch { }
    }
  };

  const healthyNodesCount = nodes.filter((n) => n.healthStatus === 'Healthy').length;
  const healthPercent = Math.round((healthyNodesCount / nodes.length) * 100);
  const activeDownCount = nodes.filter((n) => n.healthStatus === 'Down').length;

  // Самый свежий инцидент для верхнего баннера
  const latestIncident = useMemo(() => {
    const list = Object.values(incidents);
    return list.length > 0 ? list[list.length - 1] : null;
  }, [incidents]);

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 flex flex-col font-sans selection:bg-zinc-800 selection:text-zinc-100 relative">
      {/* Деликатное фоновое свечение в стиле Datadog/Linear */}
      <div className="fixed inset-0 pointer-events-none bg-[radial-gradient(ellipse_80%_50%_at_50%_-10%,rgba(16,185,129,0.05),rgba(0,0,0,0))] z-0" />

      {/* Шапка дашборда */}
      <Header isConnected={isConnected} activeIncidentsCount={activeDownCount} />

      {/* Основной контент */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8 relative z-10">
        {/* Баннер активного инцидента */}
        <IncidentBanner
          incidentAlert={latestIncident}
          activeCount={activeDownCount}
          onResolve={handleResolveAll}
          onViewModal={() => {
            const downNode = nodes.find(
              (n) => n.id.toLowerCase() === latestIncident?.incident.nodeId.toLowerCase()
            );
            if (downNode) setModalNode(downNode);
          }}
        />

        {/* Сводная панель метрик системы */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
          <div className="p-4 rounded-xl bg-zinc-900/40 backdrop-blur-md border border-zinc-800/80 hover:border-zinc-700/80 transition-all duration-200 group">
            <div className="flex items-center justify-between text-zinc-400 text-xs mb-1 font-mono">
              <div className="flex items-center gap-2">
                <Server className="w-3.5 h-3.5 text-zinc-400" />
                <span>Monitored Nodes</span>
              </div>
              <span className="text-[10px] text-zinc-400 bg-zinc-800/80 px-1.5 py-0.5 rounded">Auto</span>
            </div>
            <div className="text-2xl font-bold font-mono text-zinc-100 mt-1">{nodes.length}</div>
          </div>

          <div className="p-4 rounded-xl bg-zinc-900/40 backdrop-blur-md border border-zinc-800/80 hover:border-zinc-700/80 transition-all duration-200 group">
            <div className="flex items-center justify-between text-zinc-400 text-xs mb-1 font-mono">
              <div className="flex items-center gap-2">
                <Activity className="w-3.5 h-3.5 text-emerald-400" />
                <span>Cluster Health</span>
              </div>
              <span className="text-[10px] text-emerald-400/80 bg-emerald-500/10 px-1.5 py-0.5 rounded">SLA</span>
            </div>
            <div
              className={`text-2xl font-bold font-mono mt-1 ${
                healthPercent === 100
                  ? 'text-emerald-400'
                  : healthPercent > 50
                  ? 'text-amber-400'
                  : 'text-rose-400'
              }`}
            >
              {healthPercent}%
            </div>
          </div>

          <div className="p-4 rounded-xl bg-zinc-900/40 backdrop-blur-md border border-zinc-800/80 hover:border-zinc-700/80 transition-all duration-200 group">
            <div className="flex items-center justify-between text-zinc-400 text-xs mb-1 font-mono">
              <div className="flex items-center gap-2">
                <Layers className="w-3.5 h-3.5 text-cyan-400" />
                <span>Resilience Engine</span>
              </div>
              <span className="text-[10px] text-cyan-400/80 bg-cyan-500/10 px-1.5 py-0.5 rounded">Active</span>
            </div>
            <div className="text-sm font-semibold text-zinc-200 mt-2 font-mono">Polly v8 + Redis</div>
          </div>

          <div className="p-4 rounded-xl bg-zinc-900/40 backdrop-blur-md border border-zinc-800/80 hover:border-zinc-700/80 transition-all duration-200 group">
            <div className="flex items-center justify-between text-zinc-400 text-xs mb-1 font-mono">
              <div className="flex items-center gap-2">
                <Cpu className="w-3.5 h-3.5 text-indigo-400" />
                <span>Diagnostic Engine</span>
              </div>
              <span className="text-[10px] text-indigo-400/80 bg-indigo-500/10 px-1.5 py-0.5 rounded">RCA</span>
            </div>
            <div className="text-sm font-semibold text-zinc-200 mt-2 font-mono">Autonomous RCA</div>
          </div>
        </div>

        {/* Сетка карточек микросервисов */}
        <div className="mb-6 flex items-center justify-between">
          <div>
            <h2 className="text-base font-semibold text-zinc-100 font-mono tracking-tight">
              Live Microservices Telemetry
            </h2>
            <p className="text-xs text-zinc-400 font-mono mt-0.5">
              Live WebSocket metrics stream updated every 5 seconds
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {nodes.map((node) => (
            <NodeCard
              key={node.id}
              node={node}
              onSimulateFailure={handleSimulateFailure}
              onRestoreNode={handleRestoreNode}
              onViewAnalysis={(target) => setModalNode(target)}
            />
          ))}
        </div>
      </main>

      {/* Модальное окно детального разбора инцидента */}
      <IncidentModal
        node={modalNode}
        diagnosis={modalNode ? incidents[modalNode.id.toLowerCase()]?.aiDiagnosis ?? null : null}
        isOpen={modalNode !== null}
        onClose={() => setModalNode(null)}
        onRestore={handleRestoreNode}
      />

      {/* Подвал без личных имен и лишнего шума */}
      <footer className="border-t border-zinc-900/80 py-6 text-center text-xs font-mono text-zinc-400 relative z-10">
        NexusPulse v2.0 • Distributed Observability &amp; Telemetry Platform
      </footer>
    </div>
  );
}

export default App;
