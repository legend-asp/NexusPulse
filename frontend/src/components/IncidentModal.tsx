import React from 'react';
import type { MonitoredNode, AiDiagnosis } from '../types';
import { AlertTriangle, X, Sparkles, RotateCcw, Terminal, ExternalLink } from 'lucide-react';

interface IncidentModalProps {
  node: MonitoredNode | null;
  diagnosis: AiDiagnosis | null;
  isOpen: boolean;
  onClose: () => void;
  onRestore: (nodeId: string) => void;
}

export const IncidentModal: React.FC<IncidentModalProps> = ({
  node,
  diagnosis,
  isOpen,
  onClose,
  onRestore,
}) => {
  if (!isOpen || !node) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4 overflow-y-auto animate-in fade-in duration-200">
      <div className="relative w-full max-w-2xl rounded-2xl border border-rose-500/40 bg-slate-900 shadow-2xl shadow-rose-950/60 p-6 my-8">
        {/* Кнопка закрытия крестиком */}
        <button
          onClick={onClose}
          className="absolute top-5 right-5 p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
        >
          <X className="w-5 h-5" />
        </button>

        {/* Заголовок инцидента */}
        <div className="flex items-start gap-4 mb-6">
          <div className="p-3 rounded-xl bg-rose-500/20 text-rose-400 border border-rose-500/30 flex-shrink-0 animate-pulse">
            <AlertTriangle className="w-7 h-7" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-[10px] font-bold uppercase tracking-wider text-rose-400 bg-rose-500/10 px-2.5 py-0.5 rounded-full border border-rose-500/30">
                Active Outage Incident
              </span>
              <span className="text-xs font-mono text-slate-400">HTTP 504 • Circuit Breaker Open</span>
            </div>
            <h2 className="text-xl font-bold text-slate-100 mt-1">{node.name}</h2>
            <a
              href={node.targetUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 text-xs text-slate-400 hover:text-emerald-400 transition font-mono mt-0.5"
            >
              <span>{node.targetUrl}</span>
              <ExternalLink className="w-3 h-3" />
            </a>
          </div>
        </div>

        {/* Блок с детальным разбором от AI RCA Engine */}
        <div className="rounded-xl border border-indigo-500/30 bg-slate-950/80 p-5 mb-6">
          <div className="flex items-center gap-2 mb-4 text-indigo-400 font-semibold text-xs tracking-wider">
            <Sparkles className="w-4 h-4 text-indigo-400 animate-pulse" />
            <span>AUTONOMOUS ROOT CAUSE ANALYSIS (RCA ENGINE)</span>
          </div>

          <div className="space-y-3.5 text-xs">
            {/* 1. Root Cause */}
            <div className="p-3.5 rounded-lg bg-slate-900/80 border border-slate-800">
              <span className="text-rose-400 uppercase tracking-wider text-[10px] font-bold block mb-1">
                🔍 Root Cause Hypothesis
              </span>
              <p className="text-slate-200 leading-relaxed">
                {diagnosis?.rootCause ??
                  `The ${node.name} failed to respond within the 30-second gateway timeout threshold due to database connection pool exhaustion or an upstream deadlock.`}
              </p>
            </div>

            {/* 2. Immediate Fix */}
            <div className="p-3.5 rounded-lg bg-slate-900/80 border border-slate-800">
              <span className="text-emerald-400 uppercase tracking-wider text-[10px] font-bold block mb-1">
                ⚡ Recommended Immediate Remediation
              </span>
              <p className="text-slate-200 leading-relaxed">
                {diagnosis?.immediateFix ??
                  'Cycle container instances, kill long-running blocked transactions, and verify network connectivity.'}
              </p>
            </div>

            {/* 3. Prevention */}
            <div className="p-3.5 rounded-lg bg-slate-900/80 border border-slate-800">
              <span className="text-cyan-400 uppercase tracking-wider text-[10px] font-bold block mb-1">
                🛡️ Long-Term Architectural Prevention
              </span>
              <p className="text-slate-200 leading-relaxed">
                {diagnosis?.prevention ??
                  'Implement Keyed Polly v8 Circuit Breaker policies, add query timeouts, and scale read-replicas.'}
              </p>
            </div>
          </div>
        </div>

        {/* Стек-трейс и лог ошибки */}
        <div className="rounded-lg bg-slate-950 border border-slate-800 p-3 mb-6 font-mono text-[11px] text-slate-400">
          <div className="flex items-center gap-2 text-slate-500 mb-1.5 text-[10px] uppercase">
            <Terminal className="w-3.5 h-3.5" />
            <span>Trace Diagnostic Stack</span>
          </div>
          <div className="text-rose-400">System.Net.Http.HttpRequestException: Connection refused (504 Gateway Timeout)</div>
          <div className="text-slate-500 mt-1">   at Polly.ResiliencePipeline.ExecuteAsync(...) in ResilienceEngine.cs:line 84</div>
          <div className="text-slate-500">   at NexusPulse.NodeHealthMonitoringWorker.ProbeHostAsync(...) in Worker.cs:line 97</div>
        </div>

        {/* Кнопки действий */}
        <div className="flex items-center justify-end gap-3 pt-2 border-t border-slate-800">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg text-xs font-semibold text-slate-400 hover:text-slate-200 hover:bg-slate-800 transition"
          >
            Close
          </button>
          <button
            onClick={() => {
              onRestore(node.id);
              onClose();
            }}
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white shadow-lg shadow-emerald-950/50 transition duration-200"
          >
            <RotateCcw className="w-4 h-4" />
            Restore & Bring Back Online
          </button>
        </div>
      </div>
    </div>
  );
};
