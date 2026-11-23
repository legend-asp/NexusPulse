import React from 'react';
import type { IncidentAlertPayload } from '../types';
import { AlertTriangle, Sparkles, CheckCircle2, ChevronRight } from 'lucide-react';

interface IncidentBannerProps {
  incidentAlert: IncidentAlertPayload | null;
  onResolve: () => void;
  onViewModal?: () => void;
}

export const IncidentBanner: React.FC<IncidentBannerProps> = ({
  incidentAlert,
  onResolve,
  onViewModal,
}) => {
  if (!incidentAlert) return null;

  const { incident, aiDiagnosis } = incidentAlert;

  return (
    <div className="rounded-xl border border-rose-500/40 bg-gradient-to-b from-rose-950/40 to-slate-900/90 p-6 shadow-2xl shadow-rose-950/50 backdrop-blur-md mb-8 animate-in fade-in slide-in-from-top-4 duration-300">
      {/* Заголовок инцидента */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-rose-500/20 pb-4">
        <div className="flex items-start sm:items-center gap-3">
          <div className="p-2.5 rounded-lg bg-rose-500/20 text-rose-400 border border-rose-500/30 flex-shrink-0 animate-pulse">
            <AlertTriangle className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold uppercase tracking-wider text-rose-400 bg-rose-500/10 px-2 py-0.5 rounded border border-rose-500/20">
                Critical Incident
              </span>
              <span className="text-xs font-mono text-slate-400">
                {incident.failureType} ({incident.statusCode ?? 504})
              </span>
            </div>
            <h2 className="text-lg font-bold text-slate-100 mt-1">
              Node <span className="text-rose-300">{incident.nodeName}</span> is DOWN
            </h2>
            <p className="text-xs text-slate-400 font-mono mt-0.5">{incident.errorMessage}</p>
          </div>
        </div>

        {/* Кнопки действий */}
        <div className="flex items-center gap-2 self-start sm:self-auto">
          {onViewModal && (
            <button
              onClick={onViewModal}
              className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-xs font-semibold bg-indigo-600/20 hover:bg-indigo-600 text-indigo-300 hover:text-white border border-indigo-500/30 transition duration-200"
            >
              <Sparkles className="w-3.5 h-3.5" />
              Detailed Breakdown
              <ChevronRight className="w-3.5 h-3.5" />
            </button>
          )}
          <button
            onClick={onResolve}
            className="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold bg-emerald-600/20 hover:bg-emerald-600 text-emerald-300 hover:text-white border border-emerald-500/30 transition duration-200"
          >
            <CheckCircle2 className="w-4 h-4" />
            Resolve All
          </button>
        </div>
      </div>

      {/* Карточка Root Cause Analysis */}
      {aiDiagnosis && (
        <div className="mt-5 rounded-lg border border-indigo-500/30 bg-slate-950/70 p-4">
          <div className="flex items-center gap-2 mb-3 text-indigo-400 font-semibold text-xs tracking-wide">
            <Sparkles className="w-4 h-4" />
            <span>AUTONOMOUS ROOT CAUSE ANALYSIS & REMEDIATION (RCA ENGINE)</span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
            {/* 1. Root Cause */}
            <div className="bg-slate-900/60 p-3 rounded-lg border border-slate-800">
              <span className="text-slate-400 uppercase tracking-wider text-[10px] font-bold block mb-1">
                Root Cause
              </span>
              <p className="text-slate-200 leading-relaxed">{aiDiagnosis.rootCause}</p>
            </div>

            {/* 2. Immediate Fix */}
            <div className="bg-slate-900/60 p-3 rounded-lg border border-slate-800">
              <span className="text-emerald-400 uppercase tracking-wider text-[10px] font-bold block mb-1">
                Immediate Fix
              </span>
              <p className="text-slate-200 leading-relaxed">{aiDiagnosis.immediateFix}</p>
            </div>

            {/* 3. Long-term Prevention */}
            <div className="bg-slate-900/60 p-3 rounded-lg border border-slate-800">
              <span className="text-cyan-400 uppercase tracking-wider text-[10px] font-bold block mb-1">
                Prevention
              </span>
              <p className="text-slate-200 leading-relaxed">{aiDiagnosis.prevention}</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
