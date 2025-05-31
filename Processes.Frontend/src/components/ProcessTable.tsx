import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  startProcess,
  cancelProcess,
  revertProcess,
  resumeProcess,
} from "../api";
import type { Process } from "../api";
import "./ProcessTable.css";

interface ProcessTableProps {
  processes: Process[];
  loading: boolean;
  error: string | null;
  onProcessAction: () => Promise<void>; // New prop for refreshing parent list
}

function ProcessTable({
  processes,
  loading,
  error,
  onProcessAction,
}: ProcessTableProps) {
  const navigate = useNavigate();
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const handleRowClick = (id: string) => {
    navigate(`/processes/${id}`);
  };

  const handleAction = async (
    action: "start" | "cancel" | "revert" | "resume",
    processId: string
  ) => {
    setActionLoadingId(processId);
    setActionError(null);
    try {
      switch (action) {
        case "start":
          await startProcess(processId);
          break;
        case "cancel":
          await cancelProcess(processId);
          break;
        case "revert":
          await revertProcess(processId);
          break;
        case "resume":
          await resumeProcess(processId);
          break;
      }
      await onProcessAction(); // Refresh the main process list
    } catch (e) {
      if (e instanceof Error) {
        setActionError(e.message);
      } else {
        setActionError(`Unknown error during ${action} action.`);
      }
    } finally {
      setActionLoadingId(null);
    }
  };

  if (loading) {
    return <div>Loading...</div>;
  }

  if (error) {
    return <div className="process-table-error">{error}</div>;
  }

  return (
    <div className="process-table-container">
      {actionError && (
        <div className="process-table-action-error">{actionError}</div>
      )}
      <table className="process-table">
        <thead className="process-table-header">
          <tr>
            <th className="process-table-th">Name</th>
            <th className="process-table-th">Status</th>
            <th className="process-table-th">Created</th>
            <th className="process-table-th">Actions</th> {/* New column */}
          </tr>
        </thead>
        <tbody>
          {processes.length === 0 ? (
            <tr>
              <td colSpan={4} className="process-table-empty">
                No processes found.
              </td>
            </tr>
          ) : (
            processes.map((proc) => (
              <tr key={proc.id} className="process-table-row">
                <td
                  className="process-table-td process-table-td-name"
                  onClick={() => handleRowClick(proc.id)}
                >
                  {proc.name}
                </td>
                <td className="process-table-td process-table-td-status">
                  {proc.status}
                </td>
                <td className="process-table-td process-table-td-created">
                  {new Date(proc.createdAt).toLocaleString()}
                </td>
                <td className="process-table-td process-table-td-actions">
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      handleAction("start", proc.id);
                    }}
                    disabled={
                      actionLoadingId === proc.id ||
                      proc.status === "Running" ||
                      proc.status === "Completed"
                    }
                    className="action-button start"
                  >
                    {actionLoadingId === proc.id ? "..." : "Start"}
                  </button>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      handleAction("cancel", proc.id);
                    }}
                    disabled={
                      actionLoadingId === proc.id ||
                      proc.status === "Completed" ||
                      proc.status === "Cancelled"
                    }
                    className="action-button cancel"
                  >
                    {actionLoadingId === proc.id ? "..." : "Cancel"}
                  </button>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      handleAction("revert", proc.id);
                    }}
                    disabled={
                      actionLoadingId === proc.id ||
                      (proc.status !== "Cancelled" &&
                        proc.status !== "Interrupted")
                    }
                    className="action-button revert"
                  >
                    {actionLoadingId === proc.id ? "..." : "Revert"}
                  </button>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      handleAction("resume", proc.id);
                    }}
                    disabled={
                      actionLoadingId === proc.id ||
                      proc.status === "Running" ||
                      proc.status === "Completed"
                    }
                    className="action-button resume"
                  >
                    {actionLoadingId === proc.id ? "..." : "Resume"}
                  </button>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

export default ProcessTable;
