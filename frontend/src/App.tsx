import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import type { MonitoredNode, MetricUpdatePayload, IncidentAlertPayload, NodeRecoveredPayload } from './types';
import { useSignalR } from './hooks/useSignalR';
import { Header } from './components/Header';
import { NodeCard } from './components/NodeCard';
import { IncidentBanner } from './components/IncidentBanner';
import { IncidentModal } from './components/IncidentModal';
import { LoginView } from './components/LoginView';
import { Activity, Layers, Server, Cpu } from 'lucide-react';

const INITIAL_NODES: MonitoredNode[] = [
  {
    id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
    name: 'VPN Web Landing',
    targetUrl: 'https://nexusvpn.net',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 24,
  },
  {
    id: 'a12bc34d-58ee-4172-b567-1e02b2c3d480',
    name: 'VPN Node 01 (NL - Amsterdam)',
    targetUrl: 'https://nl.nexusvpn.net/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 28,
  },
  {
    id: 'c98de76f-12aa-4982-c567-2e02b2c3d481',
    name: 'VPN Node 02 (DE - Frankfurt)',
    targetUrl: 'https://de.nexusvpn.net/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 22,
  },
  {
    id: 'd34ef87a-23bb-4093-d678-3e02b2c3d482',
    name: 'VPN Node 03 (FI - Helsinki)',
    targetUrl: 'https://fi.nexusvpn.net/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 35,
  },
  {
    id: 'e56fa98b-34cc-4104-e789-4e02b2c3d483',
    name: 'VPN Node 04 (SE - Stockholm)',
    targetUrl: 'https://se.nexusvpn.net/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 31,
  },
  {
    id: 'f78ab09c-45dd-4215-f890-5e02b2c3d484',
    name: 'VPN Node 05 (PL - Warsaw)',
    targetUrl: 'https://pl.nexusvpn.net/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 19,
  },
  {
    id: 'b89bc10d-56ee-4326-a901-6e02b2c3d485',
    name: 'Merchant Payment Gateway',
    targetUrl: 'https://api.cryptomus.com/v1/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 45,
  },
  {
    id: 'c90cd21e-67ff-4437-b012-7e02b2c3d486',
    name: 'Telegram Bot Endpoint',
    targetUrl: 'https://api.telegram.org',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 52,
  },
];

const getNodeDiagnosis = (node: MonitoredNode) => {
  if (node.name.includes('Landing') || node.name.includes('Web')) {
    return {
      rootCause: 'CDN edge reverse-proxy connection timeout (504): TLS handshake stalls during TLS session renegotiation under high traffic.',
      immediateFix: 'Purge edge cache zones, recycle upstream Nginx worker pools, and failover to secondary origin cluster.',
      prevention: 'Configure multi-origin health failover and automate OCSP stapling verification.'
    };
  }
  if (node.name.includes('NL') || node.name.includes('Amsterdam')) {
    return {
      rootCause: 'WireGuard / TUN network interface RX buffer ring overflow under heavy UDP torrent packet burst in Amsterdam datacenter.',
      immediateFix: 'Increase net.core.netdev_max_backlog, restart WireGuard kernel service, and cycle BGP routes.',
      prevention: 'Enforce eBPF DDoS mitigation rules and distribute peer connections over redundant virtual interfaces.'
    };
  }
  if (node.name.includes('DE') || node.name.includes('Frankfurt')) {
    return {
      rootCause: 'BGP routing blackhole detected on upstream Tier-1 transit uplink in Frankfurt (AS3320).',
      immediateFix: 'Withdraw BGP route announcement via bird daemon and reroute egress VPN traffic through alternate transit peer.',
      prevention: 'Deploy BGP auto-failover with bidirectional forwarding detection (BFD) and latency-based steering.'
    };
  }
  if (node.name.includes('FI') || node.name.includes('Helsinki')) {
    return {
      rootCause: 'OpenVPN daemon kernel crash caused by memory fragmentation under high concurrent tunnel count.',
      immediateFix: 'Restart multi-daemon OpenVPN instances and reload systemd tun/tap kernel modules.',
      prevention: 'Migrate legacy tunneled users to high-performance WireGuard protocol instances.'
    };
  }
  if (node.name.includes('SE') || node.name.includes('Stockholm')) {
    return {
      rootCause: 'High UDP packet loss (42%) on Stockholm ingress interface due to ISP fiber degradation.',
      immediateFix: 'Trip circuit breaker, notify upstream NOC, and divert client ingress to Helsinki gateway.',
      prevention: 'Deploy health-check weighted DNS load balancing across Nordic server regions.'
    };
  }
  if (node.name.includes('PL') || node.name.includes('Warsaw')) {
    return {
      rootCause: 'CPU core throttling and high packet scheduling jitter due to hypervisor noisy neighbor on VPS host.',
      immediateFix: 'Migrate active client sessions to standby VPS node and execute warm reboot on worker.',
      prevention: 'Enforce dedicated CPU core pinning (taskset) and configure node autoscaling.'
    };
  }
  if (node.name.includes('Payment') || node.name.includes('Merchant')) {
    return {
      rootCause: 'Upstream banking acquiring gateway timed out after 30000ms during credit card tokenization due to acquiring bank thread-pool exhaustion.',
      immediateFix: 'Trip Polly v8 Circuit Breaker to OPEN, failover to secondary merchant gateway, and drain socket queue.',
      prevention: 'Configure Keyed Polly v8 resilience pipeline with Jitter Exponential Retry and isolated bulkhead pools.'
    };
  }
  return {
    rootCause: 'Telegram Bot API webhook latency spike: HTTP 429 Too Many Requests due to broadcast message surge.',
    immediateFix: 'Switch bot client from push webhook to resilient Long Polling with jittered backoff.',
    prevention: 'Implement token bucket rate limiter with Redis-backed message queuing for Telegram notifications.'
  };
};

export function App() {
  const [nodes, setNodes] = useState<MonitoredNode[]>(() => {
    try {
      const saved = localStorage.getItem('nexus_pulse_vpn_nodes_v2');
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed) && parsed.length === INITIAL_NODES.length) {
          return parsed;
        }
      }
      return INITIAL_NODES;
    } catch {
      return INITIAL_NODES;
    }
  });

  const [incidents, setIncidents] = useState<Record<string, IncidentAlertPayload>>(() => {
    try {
      const saved = localStorage.getItem('nexus_pulse_vpn_incidents_v2');
      return saved ? JSON.parse(saved) : {};
    } catch {
      return {};
    }
  });

  const [modalNode, setModalNode] = useState<MonitoredNode | null>(null);
  const [auth, setAuth] = useState<{ loading: boolean; enabled: boolean; authenticated: boolean }>({
    loading: true,
    enabled: false,
    authenticated: false,
  });

  const checkAuthStatus = useCallback(async () => {
    try {
      const res = await fetch('/api/auth/status', { credentials: 'include' });
      if (res.ok) {
        const data = await res.json();
        const isAuthed = Boolean(data.authenticated);
        const isEnabled = Boolean(data.enabled);
        if (isEnabled && !isAuthed) {
          localStorage.removeItem('nexus_pulse_vpn_nodes_v2');
          localStorage.removeItem('nexus_pulse_vpn_incidents_v2');
        }
        setAuth({ loading: false, enabled: isEnabled, authenticated: isAuthed });
        return;
      }
    } catch { }
    // Local static fallback if API is offline
    setAuth({ loading: false, enabled: false, authenticated: true });
  }, []);

  useEffect(() => {
    checkAuthStatus();
  }, [checkAuthStatus]);

  // Сохранение состояния в localStorage для сохранения при обновлении F5 (только после авторизации)
  useEffect(() => {
    if (!auth.authenticated) return;
    try {
      localStorage.setItem('nexus_pulse_vpn_nodes_v2', JSON.stringify(nodes));
    } catch { }
  }, [nodes, auth.authenticated]);

  useEffect(() => {
    if (!auth.authenticated) return;
    try {
      localStorage.setItem('nexus_pulse_vpn_incidents_v2', JSON.stringify(incidents));
    } catch { }
  }, [incidents, auth.authenticated]);

  // Реф для предотвращения случайного сброса упавшей ноды фоновым SignalR-пульсом
  const incidentsRef = useRef(incidents);
  useEffect(() => {
    incidentsRef.current = incidents;
  }, [incidents]);

  // 1. Загрузка нод из бэкенда после проверки авторизации
  useEffect(() => {
    if (!auth.authenticated) return;

    fetch('/api/nodes', { credentials: 'include' })
      .then((res) => {
        if (res.status === 401) {
          setAuth({ loading: false, enabled: true, authenticated: false });
          return null;
        }
        return res.ok ? res.json() : null;
      })
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
  }, [auth.authenticated]);

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

  // 5. SignalR хук (активируется только после авторизации)
  const { isConnected } = useSignalR(
    handleMetricUpdated,
    handleIncidentAlert,
    handleNodeRecovered,
    auth.authenticated
  );

  const handleLogout = async () => {
    try {
      await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' });
    } catch { }
    localStorage.removeItem('nexus_pulse_vpn_nodes_v2');
    localStorage.removeItem('nexus_pulse_vpn_incidents_v2');
    setAuth({ loading: false, enabled: true, authenticated: false });
  };

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
      await fetch(`/api/nodes/${targetNode.id}/simulate-failure`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
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
      await fetch(`/api/nodes/${nodeId}/restore`, { method: 'POST', credentials: 'include' });
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
        await fetch(`/api/nodes/${node.id}/restore`, { method: 'POST', credentials: 'include' });
      } catch { }
    }
  };

  const healthyNodesCount = nodes.filter((n) => n.healthStatus === 'Healthy').length;
  const healthPercent = nodes.length > 0 ? Math.round((healthyNodesCount / nodes.length) * 100) : 100;
  const activeDownCount = nodes.filter((n) => n.healthStatus === 'Down').length;

  // Самый свежий инцидент для верхнего баннера
  const latestIncident = useMemo(() => {
    const list = Object.values(incidents);
    return list.length > 0 ? list[list.length - 1] : null;
  }, [incidents]);

  if (auth.loading) {
    return (
      <div className="min-h-screen bg-zinc-950 text-zinc-400 flex items-center justify-center font-mono text-xs">
        Initializing telemetry session...
      </div>
    );
  }

  if (auth.enabled && !auth.authenticated) {
    return <LoginView onSuccess={() => setAuth({ loading: false, enabled: true, authenticated: true })} />;
  }

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 flex flex-col font-sans selection:bg-zinc-800 selection:text-zinc-100 relative">
      {/* Деликатное фоновое свечение в стиле Datadog/Linear */}
      <div className="fixed inset-0 pointer-events-none bg-[radial-gradient(ellipse_80%_50%_at_50%_-10%,rgba(16,185,129,0.05),rgba(0,0,0,0))] z-0" />

      {/* Шапка дашборда */}
      <Header
        isConnected={isConnected}
        activeIncidentsCount={activeDownCount}
        authEnabled={auth.enabled}
        onLogout={handleLogout}
      />

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

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-5">
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
