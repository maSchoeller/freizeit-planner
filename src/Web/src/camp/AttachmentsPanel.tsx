import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { getAntiforgeryToken } from "../api/security";
import { authenticatedFetch as fetch } from "../api/authentication";
import type {
  AttachmentReadGrant,
  RecipeAttachment,
  RecipeAttachmentQuota,
} from "./types";
import { getJson, mutateCateringJson } from "./api";
import { QueryState } from "./ui";

export function formatFileSize(bytes: number) {
  const mebibytes = bytes / (1024 * 1024);
  if (mebibytes >= 1)
    return `${new Intl.NumberFormat("de-DE", { maximumFractionDigits: 1 }).format(mebibytes)} MiB`;
  return `${new Intl.NumberFormat("de-DE", { maximumFractionDigits: 0 }).format(bytes / 1024)} KiB`;
}

export function OwnerAttachmentsPanel({
  organizationId,
  campId,
  ownerType,
  ownerId,
  ownerName,
  ownerNoun,
  canUpload,
  canDelete = false,
}: {
  organizationId: string;
  campId?: string;
  ownerType:
    | "Recipe"
    | "MaterialRequirement"
    | "ScheduleEntry"
    | "Meal"
    | "Devotion"
    | "Note";
  ownerId: string;
  ownerName: string;
  ownerNoun:
    | "das Rezept"
    | "das Material"
    | "den Zeitplaneintrag"
    | "die Mahlzeit"
    | "die Andacht"
    | "die Notiz";
  canUpload: boolean;
  canDelete?: boolean;
}) {
  const queryClient = useQueryClient();
  const basePath = campId
    ? `/api/v1/organizations/${organizationId}/camps/${campId}/files`
    : `/api/v1/organizations/${organizationId}/recipe-files`;
  const ownerQuery = campId
    ? `ownerType=${ownerType}&ownerId=${ownerId}`
    : `ownerId=${ownerId}`;
  const attachmentQueryKey = [
    organizationId,
    campId ?? "organization",
    "files",
    ownerType,
    ownerId,
  ];
  const quotaQueryKey = [
    organizationId,
    campId ?? "organization",
    "files",
    "quota",
  ];
  const attachments = useQuery({
    queryKey: attachmentQueryKey,
    queryFn: () => getJson<RecipeAttachment[]>(`${basePath}?${ownerQuery}`),
    retry: false,
  });
  const quota = useQuery({
    queryKey: quotaQueryKey,
    queryFn: () => getJson<RecipeAttachmentQuota>(`${basePath}/quota`),
    retry: false,
  });
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [inputKey, setInputKey] = useState(0);
  const [notice, setNotice] = useState("");
  const [deletingAttachmentId, setDeletingAttachmentId] = useState<
    string | null
  >(null);
  const [deleteAttachmentConfirmed, setDeleteAttachmentConfirmed] =
    useState(false);
  const uploadAttachment = useMutation({
    mutationFn: async () => {
      if (!selectedFile) throw new Error("Wähle zuerst eine Datei aus.");
      if (selectedFile.size > 10 * 1024 * 1024)
        throw new Error("Eine Datei darf höchstens zehn MiB groß sein.");
      const token = await getAntiforgeryToken();
      const body = new FormData();
      body.append("file", selectedFile);
      const response = await fetch(`${basePath}?${ownerQuery}`, {
        method: "POST",
        credentials: "same-origin",
        headers: { "X-CSRF-TOKEN": token },
        body,
      });
      if (!response.ok) {
        const problem = (await response.json().catch(() => null)) as {
          detail?: string;
        } | null;
        throw new Error(
          problem?.detail ?? "Die Datei konnte nicht hochgeladen werden.",
        );
      }
      return (await response.json()) as RecipeAttachment;
    },
    onSuccess: async (uploaded) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: attachmentQueryKey }),
        queryClient.invalidateQueries({ queryKey: quotaQueryKey }),
      ]);
      setNotice(`${uploaded.originalFileName} wurde sicher hochgeladen.`);
      setSelectedFile(null);
      setInputKey((current) => current + 1);
    },
  });
  const openAttachment = useMutation({
    mutationFn: async (attachment: RecipeAttachment) => {
      const viewer = window.open("", "_blank", "noopener,noreferrer");
      try {
        if (!viewer)
          throw new Error(
            "Die Datei konnte nicht geöffnet werden. Erlaube Pop-ups für diese Seite und versuche es erneut.",
          );
        const token = await getAntiforgeryToken();
        const response = await fetch(
          `${basePath}/${attachment.id}/read-grant`,
          {
            method: "POST",
            credentials: "same-origin",
            headers: { "X-CSRF-TOKEN": token },
          },
        );
        if (!response.ok) {
          const problem = (await response.json().catch(() => null)) as {
            detail?: string;
          } | null;
          throw new Error(
            problem?.detail ?? "Die Datei konnte nicht geöffnet werden.",
          );
        }
        const grant = (await response.json()) as AttachmentReadGrant;
        viewer.location.href = `${basePath}/content?token=${encodeURIComponent(grant.token)}`;
        return attachment;
      } catch (error) {
        viewer?.close();
        throw error;
      }
    },
  });
  const deleteAttachment = useMutation({
    mutationFn: async (attachment: RecipeAttachment) => {
      await mutateCateringJson<void>(
        `${basePath}/${attachment.id}`,
        "DELETE",
        {},
        attachment.version,
        "Die Datei wurde zwischenzeitlich geändert. Lade den aktuellen Stand erneut.",
      );
      return attachment;
    },
    onSuccess: (deleted) => {
      queryClient.setQueryData<RecipeAttachment[]>(
        attachmentQueryKey,
        (current) =>
          current?.filter((attachment) => attachment.id !== deleted.id),
      );
      void queryClient.invalidateQueries({ queryKey: quotaQueryKey });
      setDeletingAttachmentId(null);
      setDeleteAttachmentConfirmed(false);
      setNotice(
        `${deleted.originalFileName} wurde in den Papierkorb verschoben.`,
      );
    },
  });

  return (
    <section
      className="recipe-attachments"
      aria-label={`Dateien zu ${ownerName}`}
    >
      <div className="section-heading">
        <div>
          <h3>Dateien</h3>
          <p className="form-hint">
            PDF, JPEG, PNG oder WebP · höchstens 10 MiB pro Datei
          </p>
        </div>
        {quota.data ? (
          <p className="quota-usage">
            {formatFileSize(quota.data.usedBytes)} von{" "}
            {formatFileSize(quota.data.limitBytes)} belegt
          </p>
        ) : null}
      </div>
      <QueryState loading={attachments.isLoading} error={attachments.error} />
      {quota.error ? (
        <p role="alert" className="error-message">
          {quota.error.message}
        </p>
      ) : null}
      {attachments.data?.length ? (
        <ul className="recipe-attachment-list">
          {attachments.data.map((attachment) => (
            <li key={attachment.id}>
              <span>
                <strong>{attachment.originalFileName}</strong>
                <small>{formatFileSize(attachment.sizeBytes)}</small>
              </span>
              <div className="toolbar compact-toolbar">
                <button
                  type="button"
                  className="secondary-action"
                  disabled={
                    openAttachment.isPending &&
                    openAttachment.variables?.id === attachment.id
                  }
                  onClick={() => openAttachment.mutate(attachment)}
                >
                  {attachment.originalFileName} öffnen
                </button>
                {canDelete ? (
                  <button
                    type="button"
                    className="danger-action"
                    onClick={() => {
                      deleteAttachment.reset();
                      setDeletingAttachmentId(attachment.id);
                      setDeleteAttachmentConfirmed(false);
                      setNotice("");
                    }}
                  >
                    {attachment.originalFileName} löschen
                  </button>
                ) : null}
              </div>
              {deletingAttachmentId === attachment.id ? (
                <section
                  className="confirmation-panel full-row"
                  aria-label={`${attachment.originalFileName} löschen`}
                >
                  <p>
                    Die Datei bleibt 30 Tage im Camp-Papierkorb und kann dort
                    wiederhergestellt werden.
                  </p>
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={deleteAttachmentConfirmed}
                      onChange={(event) =>
                        setDeleteAttachmentConfirmed(event.target.checked)
                      }
                    />
                    {attachment.originalFileName} wirklich in den Papierkorb
                    verschieben
                  </label>
                  {deleteAttachment.error ? (
                    <p role="alert" className="error-message">
                      {deleteAttachment.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="button"
                      className="danger-action"
                      disabled={
                        !deleteAttachmentConfirmed || deleteAttachment.isPending
                      }
                      onClick={() => deleteAttachment.mutate(attachment)}
                    >
                      Datei in Papierkorb verschieben
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={deleteAttachment.isPending}
                      onClick={() => setDeletingAttachmentId(null)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </section>
              ) : null}
            </li>
          ))}
        </ul>
      ) : !attachments.isLoading && !attachments.error ? (
        <p className="empty-state">Noch keine Datei für {ownerNoun}.</p>
      ) : null}
      {openAttachment.error ? (
        <p role="alert" className="error-message">
          {openAttachment.error.message}
        </p>
      ) : null}
      {canUpload ? (
        <form
          className="recipe-attachment-upload"
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            uploadAttachment.mutate();
          }}
        >
          <label className="attachment-file-picker">
            <span>Datei für {ownerNoun}</span>
            <input
              key={inputKey}
              className="attachment-file-picker-input"
              type="file"
              aria-label={`Datei für ${ownerNoun}`}
              accept="application/pdf,image/jpeg,image/png,image/webp"
              onChange={(event) => {
                setSelectedFile(event.target.files?.[0] ?? null);
                setNotice("");
                uploadAttachment.reset();
              }}
            />
            <span className="attachment-file-picker-control">
              <span className="secondary-action attachment-file-picker-button">
                Datei auswählen
              </span>
              <span>{selectedFile?.name ?? "Keine Datei ausgewählt"}</span>
            </span>
          </label>
          <button
            type="submit"
            className="primary-action"
            disabled={!selectedFile || uploadAttachment.isPending}
          >
            {uploadAttachment.isPending
              ? `${selectedFile?.name ?? "Datei"} wird hochgeladen …`
              : `${selectedFile?.name ?? "Datei"} hochladen`}
          </button>
        </form>
      ) : null}
      {uploadAttachment.error ? (
        <p role="alert" className="error-message">
          {uploadAttachment.error.message}
        </p>
      ) : null}
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      <p className="muted">
        Dateien bleiben privat und werden erst nach einer aktuellen
        Berechtigungsprüfung kurzzeitig ausgeliefert. Eine Malware-Prüfung ist
        nicht enthalten; lade nur vertrauenswürdige Dateien hoch.
      </p>
    </section>
  );
}
