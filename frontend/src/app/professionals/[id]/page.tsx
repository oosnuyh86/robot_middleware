"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { get, del } from "@/lib/api";
import type { Professional } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";

export default function ProfessionalDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [professional, setProfessional] = useState<Professional | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showDelete, setShowDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!id) return;
    get<Professional>(`/professionals/${id}`)
      .then(setProfessional)
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load professional"),
      )
      .finally(() => setLoading(false));
  }, [id]);

  async function handleDelete() {
    setDeleting(true);
    try {
      await del(`/professionals/${id}`);
      router.push("/professionals");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete professional");
      setDeleting(false);
      setShowDelete(false);
    }
  }

  if (loading) {
    return (
      <div className="page-container">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  if (!professional) {
    return (
      <div className="page-container">
        <div className="error-message">{error || "Professional not found"}</div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <div className="detail-header">
        <h1>{professional.name}</h1>
        <div className="detail-header-actions">
          <Button onClick={() => router.push(`/professionals/${id}/edit`)}>
            Edit
          </Button>
          <Button variant="danger" onClick={() => setShowDelete(true)}>
            Delete
          </Button>
        </div>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="detail-container">
        <div className="detail-group">
          <label>Professional ID</label>
          <p>{professional._id}</p>
        </div>
        <div className="detail-group">
          <label>Profile Summary</label>
          <p>{professional.profile_summary || "No summary provided"}</p>
        </div>
        <div className="detail-group">
          <label>Company ID</label>
          <p>{professional.company_id}</p>
        </div>
        <div className="detail-group">
          <label>Created</label>
          <p>{new Date(professional.createdAt).toLocaleString()}</p>
        </div>
        <div className="detail-group">
          <label>Updated</label>
          <p>{new Date(professional.updatedAt).toLocaleString()}</p>
        </div>

        <Button variant="ghost" onClick={() => router.push("/professionals")}>
          Back to Professionals
        </Button>
      </div>

      <Modal
        open={showDelete}
        title="Delete Professional"
        onClose={() => setShowDelete(false)}
        onConfirm={handleDelete}
        confirmLabel="Delete"
        confirmVariant="danger"
        loading={deleting}
      >
        <p>
          Are you sure you want to delete <strong>{professional.name}</strong>?
          This action cannot be undone.
        </p>
      </Modal>
    </div>
  );
}
