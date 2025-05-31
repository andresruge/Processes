import type { Process } from "../api";
import "./ProcessTable.css";

interface ProcessTableProps {
  processes: Process[];
  loading: boolean;
  error: string | null;
}

function ProcessTable({ processes, loading, error }: ProcessTableProps) {
  if (loading) {
    return <div>Loading...</div>;
  }

  if (error) {
    return <div className="process-table-error">{error}</div>;
  }

  return (
    <table className="process-table">
      <thead className="process-table-header">
        <tr>
          <th className="process-table-th">Name</th>
          <th className="process-table-th">Status</th>
          <th className="process-table-th">Created</th>
        </tr>
      </thead>
      <tbody>
        {processes.length === 0 ? (
          <tr>
            <td colSpan={3} className="process-table-empty">
              No processes found.
            </td>
          </tr>
        ) : (
          processes.map((proc) => (
            <tr key={proc.id} className="process-table-row">
              <td className="process-table-td process-table-td-name">
                {proc.name}
              </td>
              <td className="process-table-td process-table-td-status">
                {proc.status}
              </td>
              <td className="process-table-td process-table-td-created">
                {new Date(proc.createdAt).toLocaleString()}
              </td>
            </tr>
          ))
        )}
      </tbody>
    </table>
  );
}

export default ProcessTable;
