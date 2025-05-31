import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import {
  fetchProcessById,
  fetchSubprocessesByParentId,
  startProcess,
  cancelProcess,
  revertProcess,
  resumeProcess,
} from "../api";
import type { Process, Subprocess, StepInfo } from "../api";
import "./ProcessDetailView.css";

interface ProcessDetailViewProps {
  onProcessAction: () => Promise<void>;
}

function ProcessDetailView({ onProcessAction }: ProcessDetailViewProps) {
  const { id } = useParams<{ id: string }>();
  const [process, setProcess] = useState<Process | null>(null);
  const [subprocesses, setSubprocesses] = useState<Subprocess[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<boolean>(false);

  const refreshProcessData = async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const fetchedProcess = await fetchProcessById(id);
      setProcess(fetchedProcess);
      const fetchedSubprocesses = await fetchSubprocessesByParentId(id);
      setSubprocesses(fetchedSubprocesses);
    } catch (e) {
      if (e instanceof Error) {
        setError(e.message);
      } else {
        setError("Unknown error fetching process details.");
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refreshProcessData();
  }, [id]);

  const handleAction = async (
    action: "start" | "cancel" | "revert" | "resume"
  ) => {
    if (!id) return;
    setActionLoading(true);
    setError(null);
    try {
      switch (action) {
        case "start":
          await startProcess(id);
          break;
        case "cancel":
          await cancelProcess(id);
          break;
        case "revert":
          await revertProcess(id);
          break;
        case "resume":
          await resumeProcess(id);
          break;
      }
      await refreshProcessData(); // Refresh current process data
      await onProcessAction(); // Refresh the main process list in App.tsx
    } catch (e) {
      if (e instanceof Error) {
        setError(e.message);
      } else {
        setError(`Unknown error during ${action} action.`);
      }
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="process-detail-container">Loading process details...</div>
    );
  }

  if (error) {
    return <div className="process-detail-error">{error}</div>;
  }

  if (!process) {
    return <div className="process-detail-container">Process not found.</div>;
  }

  const renderSubprocessSteps = (steps: { [key: string]: StepInfo }) => {
    return Object.entries(steps).map(([key, step]) => (
      <li key={key}>
        <strong>{step.description}</strong>: {step.status}
        {step.errorMessage && (
          <span className="step-error"> (Error: {step.errorMessage})</span>
        )}
      </li>
    ));
  };

  return (
    <div className="process-detail-container">
      <h2 className="process-detail-title">Process: {process.name}</h2>
      <p>
        <strong>ID:</strong> {process.id}
      </p>
      <p>
        <strong>Status:</strong> {process.status}
      </p>
      <p>
        <strong>Created At:</strong>{" "}
        {new Date(process.createdAt).toLocaleString()}
      </p>
      <p>
        <strong>Last Updated:</strong>{" "}
        {new Date(process.updatedAt).toLocaleString()}
      </p>
      {process.errorMessage && (
        <p className="process-error-message">
          <strong>Error:</strong> {process.errorMessage}
        </p>
      )}

      <div className="process-actions">
        <button
          onClick={() => handleAction("start")}
          disabled={
            actionLoading ||
            process.status === "Running" ||
            process.status === "Completed"
          }
          className="action-button start"
        >
          {actionLoading ? "Starting..." : "Start"}
        </button>
        <button
          onClick={() => handleAction("cancel")}
          disabled={
            actionLoading ||
            process.status === "Completed" ||
            process.status === "Cancelled"
          }
          className="action-button cancel"
        >
          {actionLoading ? "Cancelling..." : "Cancel"}
        </button>
        <button
          onClick={() => handleAction("revert")}
          disabled={
            actionLoading ||
            (process.status !== "Cancelled" && process.status !== "Interrupted")
          }
          className="action-button revert"
        >
          {actionLoading ? "Reverting..." : "Revert"}
        </button>
        <button
          onClick={() => handleAction("resume")}
          disabled={
            actionLoading ||
            process.status === "Running" ||
            process.status === "Completed"
          }
          className="action-button resume"
        >
          {actionLoading ? "Resuming..." : "Resume"}
        </button>
      </div>

      <h3 className="subprocesses-title">Subprocesses</h3>
      {subprocesses.length === 0 ? (
        <p>No subprocesses found for this process.</p>
      ) : (
        <table className="subprocesses-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Status</th>
              <th>Created</th>
              <th>Last Updated</th>
              <th>Steps</th>
            </tr>
          </thead>
          <tbody>
            {subprocesses.map((sub) => (
              <tr key={sub.id}>
                <td>{sub.id}</td>
                <td>{sub.status}</td>
                <td>{new Date(sub.createdAt).toLocaleString()}</td>
                <td>{new Date(sub.updatedAt).toLocaleString()}</td>
                <td>
                  <ul>{renderSubprocessSteps(sub.steps)}</ul>
                  {sub.errorMessage && (
                    <p className="subprocess-error-message">
                      Error: {sub.errorMessage}
                    </p>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Link to="/" className="back-link">
        Back to all processes
      </Link>
    </div>
  );
}

export default ProcessDetailView;
