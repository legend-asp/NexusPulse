import React from 'react';
import type { IncidentAlertPayload } from '../types';
import { AlertCircle, CheckCircle2, ChevronRight, FileText } from 'lucide-react';

interface IncidentBannerProps {
  incidentAlert: IncidentAlertPayload | null;
  onResolve: () => void;
  onViewModal?: () => void;
  activeCount?: number;
}

export const IncidentBanner: React.FC<IncidentBannerProps> = ({
  incidentAlert,
  onResolve,
  onViewModal,
  activeCount = 1,
}) => {
  if (!incidentAlert) return null;

  const { incident, aiDiagnosis } = incidentAlert;

  return (
    <div className="rounded-xl border border-rose-500/40 border-l-4 border-l-rose-500 bg-zinc-900/80 backdrop-blur-md p-5 mb-8 shadow-xl shadow-rose-950/20 animate-in fade-in duration-200">
      {/* Incident Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-zinc-800/80 pb-4">
        <div className="flex items-start sm:items-center gap-3.5">
          <div className="w-10 h-10 rounded-lg bg-rose-500/10 border border-rose-500/30 flex items-center justify-center text-rose-400 flex-shrink-0 shadow-inner">
            <AlertCircle className="w-5 h-5 animate-pulse" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-[10px] font-mono uppercase tracking-wider text-rose-400 bg-rose-500/10 px-2.5 py-0.5 rounded-full border border-rose-500/30 font-semibold">
                Critical Outage {activeCount > 1 ? `(${activeCount} Services Affected)` : ''}
              </span>
              <span className="text-xs font-mono text-zinc-400">
                {incident.failureType} • HTTP {incident.statusCode ?? 504}
              </span>
            </div>
            <h2 className="text-base font-semibold text-zinc-100 mt-1 font-mono tracking-tight">
              {incident.nodeName} is unreachable
            </h2>
            <p className="text-xs text-zinc-400 font-mono mt-0.5">{incident.errorMessage}</p>
          </div>
        </div>

        {/* Action Controls */}
        <div className="flex items-center gap-2 self-start sm:self-auto">
          {onViewModal && (
            <button
              type="button"
              onClick={onViewModal}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-mono text-zinc-200 bg-zinc-800/90 border border-zinc-700/80 hover:bg-zinc-750 hover:border-zinc-600 transition shadow-sm"
            >
              <FileText className="w-3.5 h-3.5 text-zinc-400" />
              Inspect Postmortem
              <ChevronRight className="w-3 h-3 text-zinc-500" />
            </button>
          )}
          <button
            type="button"
            onClick={onResolve}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-mono text-emerald-300 bg-emerald-500/10 border border-emerald-500/30 hover:bg-emerald-500/20 transition shadow-sm"
          >
            <CheckCircle2 className="w-3.5 h-3.5" />
            {activeCount > 1 ? 'Resolve All' : 'Resolve Incident'}
          </button>
        </div>
      </div>

      {/* Structured Diagnostics Summary */}
      {aiDiagnosis && (
        <div className="mt-4 grid grid-cols-1 md:grid-cols-3 gap-3 text-xs font-mono">
          {/* Root Cause */}
          <div className="bg-zinc-950/70 p-4 rounded-lg border border-zinc-800/80">
            <span className="text-rose-400 uppercase tracking-wider text-[10px] font-semibold block mb-1.5">
              Root Cause Hypothesis
            </span>
            <p className="text-zinc-300 font-sans text-xs leading-relaxed">
              {aiDiagnosis.rootCause}
            </p>
          </div>

          {/* Immediate Remediation */}
          <div className="bg-zinc-950/70 p-4 rounded-lg border border-zinc-800/80">
            <span className="text-emerald-400 uppercase tracking-wider text-[10px] font-semibold block mb-1.5">
              Immediate Mitigation
            </span>
            <p className="text-zinc-300 font-sans text-xs leading-relaxed">
              {aiDiagnosis.immediateFix}
            </p>
          </div>

          {/* Long-term Prevention */}
          <div className="bg-zinc-950/70 p-4 rounded-lg border border-zinc-800/80">
            <span className="text-zinc-400 uppercase tracking-wider text-[10px] font-semibold block mb-1.5">
              Preventative Architecture
            </span>
            <p className="text-zinc-300 font-sans text-xs leading-relaxed">
              {aiDiagnosis.prevention}
            </p>
          </div>
        </div>
      )}
    </div>
  );
};
