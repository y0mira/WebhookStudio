import {
  Activity,
  ArrowLeft,
  Check,
  ChevronLeft,
  ChevronRight,
  Clipboard,
  Copy,
  Download,
  GitCompare,
  Import,
  Menu,
  Moon,
  Plus,
  Radio,
  RotateCcw,
  Search,
  Send,
  Settings,
  Sun,
  Trash2,
  X,
} from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import {
  Link,
  Navigate,
  Route,
  Routes,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { useTranslation } from "react-i18next";
import i18n from "./i18n";
import { api, ApiError } from "./api";
import type {
  DiffItem,
  Endpoint,
  Filters,
  ReplayResult,
  RequestDetail,
  RequestSummary,
} from "./types";

const t = (key: string, options?: Record<string, unknown>) =>
  i18n.t(key, options);
const hookUrl = (slug: string) => `${location.origin}/hooks/${slug}/`;
const errorText = (e: unknown) =>
  e instanceof ApiError && e.code
    ? t(`errors.${e.code}`)
    : e instanceof Error
      ? e.message
      : t("errors.unknown");
const defaults = {
  responseStatusCode: 200,
  responseContentType: "application/json",
  responseBody: '{"received":true}',
  responseDelayMs: 0,
  retentionLimit: 500,
};
function App() {
  useTranslation();
  return (
    <Routes>
      <Route path="/" element={<EndpointList />} />
      <Route path="/endpoints/:id" element={<Workspace />} />
      <Route path="*" element={<Navigate to="/" />} />
    </Routes>
  );
}

export function EndpointList() {
  useTranslation();
  const qc = useQueryClient();
  const nav = useNavigate();
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const list = useQuery({ queryKey: ["endpoints"], queryFn: api.endpoints });
  const create = useMutation({
    mutationFn: api.createEndpoint,
    onSuccess: (e) => {
      qc.invalidateQueries({ queryKey: ["endpoints"] });
      nav(`/endpoints/${e.id}`);
    },
  });
  return (
    <main className="landing">
      <header>
        <div>
          <p className="eyebrow">{t("landing.eyebrow")}</p>
          <h1>Webhook Studio</h1>
          <p>{t("landing.intro")}</p>
        </div>
        <div className="header-actions">
          <LanguageButton />
          <ThemeButton />
        </div>
      </header>
      <section className="create-panel">
        <div>
          <h2>{t("landing.createTitle")}</h2>
          <p>{t("landing.createHelp")}</p>
        </div>
        <form
          onSubmit={(e: FormEvent) => {
            e.preventDefault();
            create.mutate({ name, slug });
          }}
        >
          <label>
            {t("landing.name")}
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              maxLength={80}
            />
          </label>
          <label>
            {t("landing.slug")}
            <input
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
              required
              pattern="[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?"
            />
          </label>
          <button disabled={create.isPending}>
            <Plus size={17} />
            {t(create.isPending ? "landing.creating" : "landing.create")}
          </button>
        </form>
        {create.isError && <Feedback error text={errorText(create.error)} />}
      </section>
      <section>
        <div className="section-title">
          <h2>{t("landing.endpoints")}</h2>
          <span>{list.data?.length ?? 0}</span>
        </div>
        {list.isLoading ? (
          <Status text={t("landing.loading")} />
        ) : list.data?.length ? (
          <div className="endpoint-grid">
            {list.data.map((e) => (
              <Link
                className="endpoint-card"
                key={e.id}
                to={`/endpoints/${e.id}`}
              >
                <span className="pulse-dot" />
                <strong>{e.name}</strong>
                <code>/hooks/{e.slug}/</code>
                <small>
                  {t("landing.retention", { count: e.retentionLimit })}
                </small>
              </Link>
            ))}
          </div>
        ) : (
          <Status text={t("landing.empty")} />
        )}
      </section>
    </main>
  );
}

export function Workspace() {
  useTranslation();
  const { id = "" } = useParams();
  const qc = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [connection, setConnection] = useState<
    "connecting" | "connected" | "disconnected" | "failed"
  >("connecting");
  const [newId, setNewId] = useState("");
  const [mobileNav, setMobileNav] = useState(false);
  const endpoint = useQuery({
    queryKey: ["endpoint", id],
    queryFn: () => api.endpoint(id),
  });
  const endpoints = useQuery({
    queryKey: ["endpoints"],
    queryFn: api.endpoints,
  });
  const runtime = useQuery({ queryKey: ["runtime"], queryFn: api.runtime });
  const filters: Filters = {
    page: Number(params.get("page") || 1),
    method: params.get("method") || "",
    statusCategory: params.get("status") || "",
    from: params.get("from") || "",
    to: params.get("to") || "",
    search: params.get("q") || "",
  };
  const selected = params.get("request") || "";
  const compare = params.getAll("compare");
  const requests = useQuery({
    queryKey: ["requests", id, filters],
    queryFn: () => api.requests(id, filters),
  });
  const update = (changes: Record<string, string>) =>
    setParams((old) => {
      const n = new URLSearchParams(old);
      Object.entries(changes).forEach(([k, v]) =>
        v ? n.set(k, v) : n.delete(k),
      );
      return n;
    });
  useEffect(() => {
    if (!selected && requests.data?.items[0])
      update({ request: requests.data.items[0].id });
  }, [requests.data]);
  useEffect(() => {
    const c = new HubConnectionBuilder()
      .withUrl("/hubs/requests")
      .withAutomaticReconnect()
      .build();
    c.onreconnecting(() => setConnection("connecting"));
    c.onreconnected(async () => {
      setConnection("connected");
      await qc.invalidateQueries({ queryKey: ["requests", id] });
    });
    c.onclose(() => setConnection("failed"));
    c.on("RequestCaptured", (item: RequestSummary) => {
      setNewId(item.id);
      qc.invalidateQueries({ queryKey: ["requests", id] });
    });
    c.start()
      .then(() => c.invoke("JoinEndpoint", id))
      .then(() => setConnection("connected"))
      .catch(() => setConnection("failed"));
    return () => {
      if (c.state !== HubConnectionState.Disconnected) void c.stop();
    };
  }, [id, qc]);
  if (endpoint.isLoading) return <Status text={t("workspace.opening")} />;
  if (endpoint.isError)
    return <Status text={errorText(endpoint.error)} error />;
  const ep = endpoint.data!;
  return (
    <main className="studio">
      <header className="studio-head">
        <button
          className="icon-button mobile-only"
          aria-label={t("workspace.openNav")}
          onClick={() => setMobileNav(!mobileNav)}
        >
          <Menu />
        </button>
        <div>
          <h1>{ep.name}</h1>
          <button
            className="copy-url"
            onClick={() => navigator.clipboard.writeText(hookUrl(ep.slug))}
          >
            <code>{hookUrl(ep.slug)}</code>
            <Copy size={15} />
          </button>
        </div>
        <Connection status={connection} />
        <div className="header-actions">
          <LanguageButton />
          <ThemeButton />
        </div>
      </header>
      {runtime.data?.allowPrivateNetworkReplay && (
        <div className="network-warning" role="alert">
          {t("workspace.privateWarning")}
        </div>
      )}
      {runtime.data?.networkExposed && (
        <div className="network-warning" role="alert">
          {t("workspace.networkWarning")}
        </div>
      )}
      <div className="studio-grid">
        <aside className={`endpoint-nav ${mobileNav ? "open" : ""}`}>
          <div className="nav-head">
            <Link to="/">
              <ArrowLeft size={16} />
              {t("workspace.endpoints")}
            </Link>
            <button
              className="icon-button mobile-only"
              aria-label={t("workspace.closeNav")}
              onClick={() => setMobileNav(false)}
            >
              <X />
            </button>
          </div>
          <nav aria-label={t("workspace.endpoints")}>
            {endpoints.data?.map((e) => (
              <Link
                key={e.id}
                className={e.id === id ? "active" : ""}
                to={`/endpoints/${e.id}`}
              >
                <span className="pulse-dot" />
                {e.name}
              </Link>
            ))}
          </nav>
          <div className="local-warning">
            <Radio size={16} />
            <span>
              {t("workspace.localOnly")}
              <br />
              <small>{t("workspace.notPublic")}</small>
            </span>
          </div>
        </aside>
        <section className="request-column">
          <FiltersBar value={filters} onChange={update} />
          <div className="stream-head">
            <strong>{t("workspace.requests")}</strong>
            <span>{requests.data?.total ?? 0}</span>
          </div>
          {requests.isLoading ? (
            <Status text={t("workspace.loading")} />
          ) : requests.data?.items.length ? (
            <ul className="request-list">
              {requests.data.items.map((r) => (
                <RequestRow
                  key={r.id}
                  item={r}
                  selected={selected === r.id}
                  fresh={newId === r.id}
                  compare={compare.includes(r.id)}
                  onSelect={() => update({ request: r.id })}
                  onCompare={() => {
                    const next = compare.includes(r.id)
                      ? compare.filter((x) => x !== r.id)
                      : [...compare, r.id].slice(-2);
                    const n = new URLSearchParams(params);
                    n.delete("compare");
                    next.forEach((x) => n.append("compare", x));
                    setParams(n);
                  }}
                />
              ))}
            </ul>
          ) : (
            <Status text={t("workspace.empty")} />
          )}
          <Pagination
            page={filters.page}
            total={requests.data?.total || 0}
            onPage={(p) => update({ page: String(p) })}
          />
        </section>
        <section className="inspector">
          {compare.length === 2 ? (
            <ComparePanel
              endpointId={id}
              ids={compare as [string, string]}
              onClose={() => {
                const n = new URLSearchParams(params);
                n.delete("compare");
                setParams(n);
              }}
            />
          ) : selected ? (
            <RequestDetailPanel id={selected} />
          ) : (
            <Status text={t("workspace.select")} />
          )}
        </section>
      </div>
      <WorkspaceActions endpoint={ep} />
      <div className="sr-live" aria-live="polite">
        {newId ? t("workspace.newRequest") : ""}
      </div>
    </main>
  );
}

function FiltersBar({
  value,
  onChange,
}: {
  value: Filters;
  onChange: (x: Record<string, string>) => void;
}) {
  return (
    <div className="filters">
      <label className="search">
        <Search size={15} />
        <span className="sr-only">{t("filters.search")}</span>
        <input
          value={value.search}
          onChange={(e) => onChange({ q: e.target.value, page: "1" })}
          placeholder={t("filters.placeholder")}
        />
      </label>
      <select
        aria-label={t("filters.method")}
        value={value.method}
        onChange={(e) => onChange({ method: e.target.value, page: "1" })}
      >
        <option value="">{t("filters.allMethods")}</option>
        {["GET", "POST", "PUT", "PATCH", "DELETE"].map((x) => (
          <option key={x}>{x}</option>
        ))}
      </select>
      <select
        aria-label={t("filters.status")}
        value={value.statusCategory}
        onChange={(e) => onChange({ status: e.target.value, page: "1" })}
      >
        <option value="">{t("filters.allStatus")}</option>
        {[2, 3, 4, 5].map((x) => (
          <option key={x} value={x}>
            {x}xx
          </option>
        ))}
      </select>
      <label>
        <span>{t("filters.from")}</span>
        <input
          type="datetime-local"
          value={value.from}
          onChange={(e) => onChange({ from: e.target.value, page: "1" })}
        />
      </label>
      <label>
        <span>{t("filters.to")}</span>
        <input
          type="datetime-local"
          value={value.to}
          onChange={(e) => onChange({ to: e.target.value, page: "1" })}
        />
      </label>
    </div>
  );
}
function RequestRow({
  item,
  selected,
  fresh,
  compare,
  onSelect,
  onCompare,
}: {
  item: RequestSummary;
  selected: boolean;
  fresh: boolean;
  compare: boolean;
  onSelect: () => void;
  onCompare: () => void;
}) {
  return (
    <li className={fresh ? "fresh" : ""}>
      <button className={selected ? "selected" : ""} onClick={onSelect}>
        <span className={`method method-${item.method.toLowerCase()}`}>
          {item.method}
        </span>
        <span className="request-path">{item.pathAndQuery}</span>
        <time title={item.receivedAtUtc}>
          {new Intl.DateTimeFormat(i18n.language, {
            timeStyle: "medium",
          }).format(new Date(item.receivedAtUtc))}
        </time>
        <small>
          {item.responseStatusCode} · {item.bodySize} B
        </small>
      </button>
      <button
        className={`compare-check ${compare ? "checked" : ""}`}
        aria-label={t(compare ? "comparison.remove" : "comparison.add")}
        onClick={onCompare}
      >
        {compare ? <Check size={15} /> : <GitCompare size={15} />}
      </button>
    </li>
  );
}
function Pagination({
  page,
  total,
  onPage,
}: {
  page: number;
  total: number;
  onPage: (x: number) => void;
}) {
  if (total <= 25) return null;
  return (
    <div className="pagination">
      <button disabled={page <= 1} onClick={() => onPage(page - 1)}>
        <ChevronLeft />
        {t("comparison.previous")}
      </button>
      <span>{t("comparison.page", { page })}</span>
      <button disabled={page * 25 >= total} onClick={() => onPage(page + 1)}>
        {t("comparison.next")}
        <ChevronRight />
      </button>
    </div>
  );
}

export function RequestDetailPanel({ id }: { id: string }) {
  useTranslation();
  const detail = useQuery({
    queryKey: ["request", id],
    queryFn: () => api.request(id),
  });
  const [target, setTarget] = useState("");
  const [expanded, setExpanded] = useState(false);
  const [feedback, setFeedback] = useState("");
  const replay = useMutation({
    mutationFn: () => api.replay(id, target),
    onSuccess: (r) =>
      setFeedback(
        r.succeeded
          ? t("detail.received", {
              status: r.statusCode,
              duration: r.durationMs,
            })
          : t("detail.failed", {
              error:
                r.code && i18n.exists(`errors.${r.code}`)
                  ? t(`errors.${r.code}`)
                  : r.error,
            }),
      ),
  });
  if (detail.isLoading) return <Status text={t("detail.reading")} />;
  if (detail.isError) return <Status text={errorText(detail.error)} error />;
  const r = detail.data!;
  const body = decodeBody(r);
  const visible =
    body.text.length > 10000 && !expanded
      ? body.text.slice(0, 10000)
      : body.text;
  return (
    <div className="detail">
      <div className="detail-head">
        <div>
          <span className={`method method-${r.method.toLowerCase()}`}>
            {r.method}
          </span>
          <h2>{r.pathAndQuery}</h2>
          <p>
            <time title={r.receivedAtUtc}>
              {new Intl.DateTimeFormat(i18n.language, {
                dateStyle: "short",
                timeStyle: "medium",
              }).format(new Date(r.receivedAtUtc))}
            </time>{" "}
            · {r.remoteIp || t("common.unknownIp")} ·{" "}
            {new Intl.NumberFormat(i18n.language).format(r.bodySize)}{" "}
            {t("common.bytes")}
          </p>
        </div>
        <div className="toolbar">
          <CopyButton
            text={toInfo(r, body.text)}
            label={t("detail.copyInfo")}
          />
          <CopyButton text={toCurl(r)} label={t("detail.copyCurl")} />
          <a
            className="button secondary"
            href={`/api/requests/${id}/export?format=har`}
            download
          >
            <Download size={16} />
            {t("detail.har")}
          </a>
        </div>
      </div>
      <DataBlock title={t("detail.headers")}>
        <dl className="headers">
          {Object.entries(r.headers).map(([k, v]) => (
            <div key={k}>
              <dt>{k}</dt>
              <dd>{v.join(", ")}</dd>
            </div>
          ))}
        </dl>
      </DataBlock>
      <DataBlock title={t("detail.body")}>
        {body.binary ? (
          <p>{t("detail.binary", { count: r.bodySize })}</p>
        ) : (
          <>
            <pre>{visible || t("detail.emptyBody")}</pre>
            {body.text.length > 10000 && (
              <button
                className="secondary"
                onClick={() => setExpanded(!expanded)}
              >
                {t(expanded ? "detail.collapse" : "detail.showFull")}
              </button>
            )}
          </>
        )}
      </DataBlock>
      <section className="replay">
        <h3>{t("detail.replayTitle")}</h3>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            replay.mutate();
          }}
        >
          <label>
            {t("detail.target")}
            <input
              type="url"
              value={target}
              onChange={(e) => setTarget(e.target.value)}
              required
              placeholder="http://localhost:9000/receive"
            />
          </label>
          <button disabled={replay.isPending}>
            <Send size={16} />
            {t(replay.isPending ? "detail.replaying" : "detail.replay")}
          </button>
        </form>
        {feedback && (
          <Feedback text={feedback} error={!replay.data?.succeeded} />
        )}
      </section>
    </div>
  );
}
function ComparePanel({
  endpointId,
  ids,
  onClose,
}: {
  endpointId: string;
  ids: [string, string];
  onClose: () => void;
}) {
  const q = useQuery({
    queryKey: ["compare", ...ids],
    queryFn: () => api.compare(endpointId, ids[0], ids[1]),
  });
  return (
    <div className="compare-panel">
      <div className="panel-title">
        <div>
          <p className="eyebrow">{t("comparison.eyebrow")}</p>
          <h2>{t("comparison.title")}</h2>
        </div>
        <button
          className="icon-button"
          aria-label={t("comparison.close")}
          onClick={onClose}
        >
          <X />
        </button>
      </div>
      {q.isLoading ? (
        <Status text={t("comparison.loading")} />
      ) : q.data?.differences.length ? (
        <table>
          <thead>
            <tr>
              <th>{t("comparison.meaning")}</th>
              <th>{t("comparison.field")}</th>
              <th>{t("comparison.left")}</th>
              <th>{t("comparison.right")}</th>
            </tr>
          </thead>
          <tbody>
            {q.data.differences.map((d, i) => (
              <tr key={i}>
                <td>
                  <span className={`diff-kind ${d.kind}`}>
                    {t(`comparison.${d.kind}`)}
                  </span>
                </td>
                <td>
                  <code>{d.path}</code>
                </td>
                <td>
                  <code>{d.left ?? "—"}</code>
                </td>
                <td>
                  <code>{d.right ?? "—"}</code>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <Status text={t("comparison.identical")} />
      )}
    </div>
  );
}

function WorkspaceActions({ endpoint }: { endpoint: Endpoint }) {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [notice, setNotice] = useState("");
  const file = useRef<HTMLInputElement>(null);
  const settingsButton = useRef<HTMLButtonElement>(null);
  const closeSettings = () => {
    setOpen(false);
    requestAnimationFrame(() => settingsButton.current?.focus());
  };
  const clear = useMutation({
    mutationFn: () => api.clear(endpoint.id),
    onSuccess: (r) => {
      setNotice(t("actions.cleared", { count: r.deleted }));
      qc.invalidateQueries({ queryKey: ["requests", endpoint.id] });
    },
  });
  return (
    <div className="workspace-actions">
      <button
        ref={settingsButton}
        className="secondary"
        onClick={() => setOpen(true)}
      >
        <Settings size={16} />
        {t("actions.settings")}
      </button>
      <a
        className="button secondary"
        href={`/api/endpoints/${endpoint.id}/export`}
        download
      >
        <Download size={16} />
        {t("actions.export")}
      </a>
      <button className="secondary" onClick={() => file.current?.click()}>
        <Import size={16} />
        {t("actions.import")}
      </button>
      <input
        ref={file}
        hidden
        type="file"
        accept="application/json"
        onChange={async (e) => {
          const f = e.target.files?.[0];
          if (f)
            try {
              const r = await api.importPackage(endpoint.id, f);
              setNotice(t("actions.imported", { count: r.imported }));
              qc.invalidateQueries({ queryKey: ["requests", endpoint.id] });
            } catch (x) {
              setNotice(errorText(x));
            }
        }}
      />
      <button
        className="danger"
        onClick={() => confirm(t("actions.confirmClear")) && clear.mutate()}
      >
        <Trash2 size={16} />
        {t("actions.clear")}
      </button>
      {notice && (
        <div className="toast" role="status">
          {notice}
        </div>
      )}
      {open && <SettingsDialog endpoint={endpoint} onClose={closeSettings} />}
    </div>
  );
}
function SettingsDialog({
  endpoint,
  onClose,
}: {
  endpoint: Endpoint;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const dialog = useRef<HTMLDivElement>(null);
  const first = useRef<HTMLInputElement>(null);
  const [form, setForm] = useState({ ...endpoint });
  const save = useMutation({
    mutationFn: () => api.settings(endpoint.id, form),
    onSuccess: (e) => {
      qc.setQueryData(["endpoint", endpoint.id], e);
      onClose();
    },
  });
  useEffect(() => {
    first.current?.focus();
    const key = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
      if (e.key === "Tab") {
        const items = [
          ...dialog.current!.querySelectorAll<HTMLElement>(
            "button,input,textarea",
          ),
        ].filter((x) => !x.hasAttribute("disabled"));
        if (!items.length) return;
        const edge = e.shiftKey ? items[0] : items[items.length - 1];
        if (document.activeElement === edge) {
          e.preventDefault();
          (e.shiftKey ? items[items.length - 1] : items[0])?.focus();
        }
      }
    };
    addEventListener("keydown", key);
    return () => removeEventListener("keydown", key);
  }, [onClose]);
  return (
    <div
      className="dialog-backdrop"
      onMouseDown={(e) => e.target === e.currentTarget && onClose()}
    >
      <div
        ref={dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby="settings-title"
        className="dialog"
      >
        <div className="panel-title">
          <h2 id="settings-title">{t("settings.title")}</h2>
          <div className="dialog-title-actions">
            <LanguageButton />
            <button
              className="icon-button"
              aria-label={t("settings.close")}
              onClick={onClose}
            >
              <X />
            </button>
          </div>
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            save.mutate();
          }}
        >
          <label>
            {t("settings.status")}
            <input
              ref={first}
              type="number"
              min="100"
              max="599"
              value={form.responseStatusCode}
              onChange={(e) =>
                setForm({ ...form, responseStatusCode: +e.target.value })
              }
            />
          </label>
          <label>
            {t("settings.contentType")}
            <input
              value={form.responseContentType}
              onChange={(e) =>
                setForm({ ...form, responseContentType: e.target.value })
              }
            />
          </label>
          <label>
            {t("settings.body")}
            <textarea
              maxLength={65536}
              value={form.responseBody}
              onChange={(e) =>
                setForm({ ...form, responseBody: e.target.value })
              }
            />
          </label>
          <label>
            {t("settings.delay")}
            <input
              type="number"
              min="0"
              max="10000"
              value={form.responseDelayMs}
              onChange={(e) =>
                setForm({ ...form, responseDelayMs: +e.target.value })
              }
            />
          </label>
          <label>
            {t("settings.retention")}
            <input
              type="number"
              min="10"
              max="10000"
              value={form.retentionLimit}
              onChange={(e) =>
                setForm({ ...form, retentionLimit: +e.target.value })
              }
            />
          </label>
          <div className="dialog-actions">
            <button
              type="button"
              className="secondary"
              onClick={() => setForm({ ...form, ...defaults })}
            >
              <RotateCcw size={16} />
              {t("settings.defaults")}
            </button>
            <button disabled={save.isPending}>
              {t(save.isPending ? "settings.saving" : "settings.save")}
            </button>
          </div>
          {save.isError && <Feedback error text={errorText(save.error)} />}
        </form>
      </div>
    </div>
  );
}

function LanguageButton() {
  return (
    <button
      className="language-button secondary"
      aria-label={t("language.label")}
      onClick={() =>
        void i18n.changeLanguage(i18n.language === "zh-CN" ? "en-US" : "zh-CN")
      }
    >
      {t("language.switch")}
    </button>
  );
}
function ThemeButton() {
  const [dark, setDark] = useState(() => localStorage.theme !== "light");
  useEffect(() => {
    document.documentElement.dataset.theme = dark ? "dark" : "light";
    localStorage.theme = dark ? "dark" : "light";
  }, [dark]);
  return (
    <button
      className="icon-button"
      aria-label={t(dark ? "theme.light" : "theme.dark")}
      onClick={() => setDark(!dark)}
    >
      {dark ? <Sun /> : <Moon />}
    </button>
  );
}
function Connection({ status }: { status: string }) {
  return (
    <span className={`connection ${status}`}>
      <span />
      {t(`connection.${status}`)}
    </span>
  );
}
function CopyButton({ text, label }: { text: string; label: string }) {
  const [done, setDone] = useState(false);
  return (
    <button
      className="secondary"
      onClick={async () => {
        await navigator.clipboard.writeText(text);
        setDone(true);
        setTimeout(() => setDone(false), 1500);
      }}
    >
      {done ? <Check size={16} /> : <Clipboard size={16} />}{" "}
      {done ? t("detail.copied") : label}
    </button>
  );
}
function DataBlock({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="data-block">
      <h3>{title}</h3>
      {children}
    </section>
  );
}
function Status({ text, error = false }: { text: string; error?: boolean }) {
  return (
    <div
      className={`status ${error ? "error" : ""}`}
      role={error ? "alert" : "status"}
    >
      <Activity size={18} />
      <p>{text}</p>
    </div>
  );
}
function Feedback({ text, error = false }: { text: string; error?: boolean }) {
  return (
    <p
      className={`feedback ${error ? "error" : "success"}`}
      role={error ? "alert" : "status"}
    >
      {error ? <X size={15} /> : <Check size={15} />} {text}
    </p>
  );
}
function decodeBody(r: RequestDetail) {
  try {
    const bytes = Uint8Array.from(atob(r.bodyBase64), (c) => c.charCodeAt(0));
    const text = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
    if (r.contentType?.includes("json"))
      try {
        return {
          text: JSON.stringify(JSON.parse(text), null, 2),
          binary: false,
        };
      } catch {}
    return { text, binary: false };
  } catch {
    return { text: "", binary: true };
  }
}
function toInfo(r: RequestDetail, body = "") {
  return `${r.method} ${r.pathAndQuery}\nReceived: ${r.receivedAtUtc}\n${Object.entries(
    r.headers,
  )
    .filter(([k]) => !["authorization", "cookie"].includes(k.toLowerCase()))
    .map(([k, v]) => `${k}: ${v.join(", ")}`)
    .join("\n")}\n\n${body}`;
}
function toCurl(r: RequestDetail) {
  const headers = Object.entries(r.headers)
    .filter(
      ([k]) =>
        !["host", "content-length", "authorization", "cookie"].includes(
          k.toLowerCase(),
        ),
    )
    .map(([k, v]) => `-H '${k}: ${v.join(", ")}'`)
    .join(" ");
  const body = decodeBody(r);
  return `curl -X ${r.method} ${headers} ${body.binary || !body.text ? "" : `--data-raw '${body.text.split("'").join("'\\''")}'`} 'TARGET_URL'`;
}
export default App;
