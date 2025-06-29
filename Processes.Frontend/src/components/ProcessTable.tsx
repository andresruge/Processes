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
  type ActionType = "start" | "cancel" | "revert" | "resume";
  const [loadingAction, setLoadingAction] = useState<{
    processId: string;
    action: ActionType;
  } | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const actionConfig: {
    action: ActionType;
    label: string;
    loadingLabel: string;
    className: string;
    isActionDisabled: (status: string) => boolean;
  }[] = [
    {
      action: "start",
      label: "Start",
      loadingLabel: "Starting...",
      className: "start",
      isActionDisabled: (status) =>
        status === "Running" || status === "Completed",
    },
    {
      action: "cancel",
      label: "Cancel",
      loadingLabel: "Cancelling...",
      className: "cancel",
      isActionDisabled: (status) =>
        status === "Completed" || status === "Cancelled",
    },
    {
      action: "revert",
      label: "Revert",
      loadingLabel: "Reverting...",
      className: "revert",
      isActionDisabled: (status) =>
        status !== "Cancelled" && status !== "Interrupted",
    },
    {
      action: "resume",
      label: "Resume",
      loadingLabel: "Resuming...",
      className: "resume",
      isActionDisabled: (status) =>
        status === "Running" || status === "Completed",
    },
  ];

  const getStatusClass = (status: string) => {
    switch (status) {
      case "Running":
        return "status-running";
      case "Completed":
        return "status-completed";
      case "Cancelled":
        return "status-cancelled";
      case "Interrupted":
        return "status-interrupted";
      default:
        return "status-pending";
    }
  };

  const handleRowClick = (id: string) => {
    navigate(`/processes/${id}`);
  };

  const handleAction = async (action: ActionType, processId: string) => {
    setLoadingAction({ processId, action });
    setActionError(null);
    try {
      const actions: Record<ActionType, (id: string) => Promise<void>> = {
        start: startProcess,
        cancel: cancelProcess,
        revert: revertProcess,
        resume: resumeProcess,
      };
      await actions[action](processId);
      await onProcessAction(); // Refresh the main process list
    } catch (e) {
      if (e instanceof Error) {
        setActionError(e.message);
      } else {
        setActionError(`Unknown error during ${action} action.`);
      }
    } finally {
      setLoadingAction(null);
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
      {actionError && <div className="process-table-error">{actionError}</div>}
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
            processes.map((proc) => {
              const isAnyActionLoading = loadingAction?.processId === proc.id;
              return (
                <tr
                  key={proc.id}
                  className="process-table-row"
                  onClick={() => handleRowClick(proc.id)}
                >
                  <td className="process-table-td process-table-td-name">
                    {proc.name}
                  </td>
                  <td className="process-table-td">
                    <span
                      className={`status-badge ${getStatusClass(proc.status)}`}
                    >
                      {proc.status}
                    </span>
                  </td>
                  <td className="process-table-td process-table-td-created">
                    {new Date(proc.createdAt).toLocaleString()}
                  </td>
                  <td className="process-table-td process-table-td-actions">
                    {actionConfig.map(
                      ({
                        action,
                        label,
                        loadingLabel,
                        className,
                        isActionDisabled,
                      }) => {
                        const isLoading =
                          isAnyActionLoading && loadingAction.action === action;
                        return (
                          <button
                            key={action}
                            onClick={(e) => {
                              e.stopPropagation();
                              handleAction(action, proc.id);
                            }}
                            disabled={
                              isAnyActionLoading ||
                              isActionDisabled(proc.status)
                            }
                            className={`action-button ${className}`}
                          >
                            {isLoading ? loadingLabel : label}
                          </button>
                        );
                      }
                    )}
                  </td>
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}

export default ProcessTable;
