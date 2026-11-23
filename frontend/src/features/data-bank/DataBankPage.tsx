// @ts-nocheck
/* eslint-disable react-hooks/set-state-in-effect, react-hooks/exhaustive-deps */
import { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  DndContext, PointerSensor, closestCenter, useSensor, useSensors,
} from '@dnd-kit/core';
import {
  SortableContext, arrayMove, useSortable,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import {
  AlertTriangle, ArchiveRestore, CheckCircle2, Download, FileSpreadsheet,
  Filter, History, Loader2, Plus, RefreshCw, Save, ShieldAlert, Trash2, Upload
} from 'lucide-react';
import Button from '../../components/ui/Button';
import EmptyState from '../../components/ui/EmptyState';
import Modal from '../../components/ui/Modal';
import { dataBankApi } from '../../api/dataBankApi';
import { useTranslation } from 'react-i18next';

const mappingActions = ['MAP_TO_EXISTING', 'ADD_NEW_COLUMN', 'IGNORE_COLUMN', 'RENAME_AND_ADD'];
const systemColumns = ['RollNumber', 'FullName', 'Email', 'Major'];
const datasetTypes = ['STUDENT_LIST', 'TEAM_ASSIGNMENT', 'CHECKPOINT', 'EVALUATION', 'PROPOSAL', 'MENTORING', 'CUSTOM'];
const groupStorageKey = 'dataBankGridGroupColumn';

const unwrap = (res) => res?.data || res;

const formatDateTime = (value) => {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleString('vi-VN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
};

const SortableHeaderCell = ({ id, children, className }) => {
  const { t } = useTranslation('community');
  const { attributes, listeners, setNodeRef, transform, transition } = useSortable({ id });
  return (
    <th
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={className}
    >
      <div className="flex items-start gap-2">
        <span
          {...attributes}
          {...listeners}
          className="mt-0.5 cursor-grab select-none rounded border border-slate-200 bg-white px-1 text-overline text-slate-400 active:cursor-grabbing"
          title={t('dataBankPage.dragColumn')}
        >
          ::
        </span>
        {children}
      </div>
    </th>
  );
};

const DataBankPage = () => {
  const { t } = useTranslation('community');
  const location = useLocation();
  const [file, setFile] = useState(null);
  const [sheets, setSheets] = useState([]);
  const [sheetName, setSheetName] = useState('');
  const [headerRow, setHeaderRow] = useState(1);
  const [scope, setScope] = useState({
    semester: location.state?.semester || 'SU26',
    subjectCode: location.state?.subjectCode || 'EXE101',
    classCode: location.state?.classCode || '',
    datasetType: 'CUSTOM',
  });
  const [classes, setClasses] = useState([]);
  const [selectedClassId, setSelectedClassId] = useState(location.state?.classId || '');
  const [columns, setColumns] = useState([]);
  const [preview, setPreview] = useState(null);
  const [mappings, setMappings] = useState([]);
  const [datasets, setDatasets] = useState([]);
  const [gridDatasets, setGridDatasets] = useState([]);
  const [history, setHistory] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [exportColumns, setExportColumns] = useState(['RollNumber', 'FullName', 'Email', 'Major']);
  const [selectedExportColumns, setSelectedExportColumns] = useState(['RollNumber', 'FullName', 'Email', 'Major']);
  const [exportPreviewRows, setExportPreviewRows] = useState([]);
  const [busy, setBusy] = useState('');
  const [loadingData, setLoadingData] = useState(true);
  const [confirmHighConflicts, setConfirmHighConflicts] = useState(false);
  const [columnFilters, setColumnFilters] = useState({});
  const [sortConfig, setSortConfig] = useState({ key: 'RollNumber', direction: 'asc' });
  const [editingCell, setEditingCell] = useState(null);
  const [editingValue, setEditingValue] = useState('');
  const [selectedCell, setSelectedCell] = useState(null);
  const [fillRange, setFillRange] = useState(null);
  const [groupColumnInput, setGroupColumnInput] = useState(() => localStorage.getItem(groupStorageKey) || '');
  const [groupColumn, setGroupColumn] = useState(() => localStorage.getItem(groupStorageKey) || '');
  const [rememberGrouping, setRememberGrouping] = useState(() => Boolean(localStorage.getItem(groupStorageKey)));
  const [textDialog, setTextDialog] = useState(null);
  const [confirmDialog, setConfirmDialog] = useState(null);
  const loadRequestRef = useRef(0);
  const pendingTemplateRef = useRef(null);
  const savingCellRef = useRef(false);
  const fillDragRef = useRef(null);
  const sensors = useSensors(useSensor(PointerSensor));

  const classFilters = useMemo(() => (
    selectedClassId ? { classId: selectedClassId, limit: 500 } : { limit: 200 }
  ), [selectedClassId]);

  const gridFilters = useMemo(() => (
    selectedClassId ? { classId: selectedClassId, limit: 1000 } : { limit: 200 }
  ), [selectedClassId]);

  const requestText = ({ title, label, defaultValue = '', placeholder = '', required = false }) => new Promise((resolve) => {
    setTextDialog({ title, label, value: defaultValue, placeholder, required, resolve });
  });

  const requestConfirm = ({ title, description, confirmText = 'Confirm', variant = 'danger' }) => new Promise((resolve) => {
    setConfirmDialog({ title, description, confirmText, variant, resolve });
  });

  const closeTextDialog = (value = null) => {
    textDialog?.resolve(value);
    setTextDialog(null);
  };

  const closeConfirmDialog = (value = false) => {
    confirmDialog?.resolve(value);
    setConfirmDialog(null);
  };

  const loadReferenceData = async ({ showLoading = false } = {}) => {
    const requestId = loadRequestRef.current + 1;
    loadRequestRef.current = requestId;
    if (showLoading) setLoadingData(true);
    try {
      const [classRes, colRes, historyRes, templateRes, dataRes, gridRes] = await Promise.all([
        dataBankApi.getClasses(),
        dataBankApi.getColumns(),
        dataBankApi.getHistory(selectedClassId ? { classId: selectedClassId } : undefined),
        dataBankApi.getTemplates(),
        dataBankApi.getDatasets(classFilters),
        dataBankApi.getDatasets(gridFilters),
      ]);
      if (requestId !== loadRequestRef.current) return;
      setClasses(unwrap(classRes).classes || []);
      setColumns(unwrap(colRes).columns || []);
      setHistory(unwrap(historyRes).history || []);
      setTemplates(unwrap(templateRes).templates || []);
      setDatasets(unwrap(dataRes).datasets || []);
      setGridDatasets(unwrap(gridRes).datasets || []);
    } catch (err) {
      if (requestId === loadRequestRef.current) {
        toast.error(err.message || t('dataBankPage.failedToLoadDataBank'));
      }
    } finally {
      if (requestId === loadRequestRef.current && showLoading) setLoadingData(false);
    }
  };

  useEffect(() => {
    loadReferenceData({ showLoading: true });
  }, [selectedClassId, classFilters, gridFilters]);

  useEffect(() => {
    if (location.state?.classId) setSelectedClassId(location.state.classId);
    if (location.state?.classCode) {
      setScope((current) => ({
        ...current,
        classCode: location.state.classCode || current.classCode,
        subjectCode: location.state.subjectCode || current.subjectCode,
        semester: location.state.semester || current.semester,
      }));
    }
  }, [location.state]);

  const applyClassToScope = (classId) => {
    setSelectedClassId(classId);
    const cls = classes.find((item) => item._id === classId);
    if (!cls) {
      setScope((current) => ({ ...current, classId: '', classCode: '', subjectCode: '', semester: '' }));
      setPreview(null);
      setMappings([]);
      setExportPreviewRows([]);
      return;
    }
    setScope((current) => ({
      ...current,
      classId: cls._id,
      classCode: cls.classCode || '',
      subjectCode: cls.subjectCode || '',
      semester: `${cls.semester || ''}${String(cls.year || '').slice(-2)}`,
    }));
    setPreview(null);
    setMappings([]);
    setConfirmHighConflicts(false);
    setExportPreviewRows([]);
  };

  const spreadsheetColumns = useMemo(() => {
    const keys = new Set(systemColumns);
    columns.forEach((column) => {
      if (column?.key) keys.add(column.key);
    });
    const seenByRoll = new Map();
    gridDatasets.forEach((dataset) => {
      const rollNumber = dataset.rollNumber || dataset._id;
      if (!seenByRoll.has(rollNumber)) seenByRoll.set(rollNumber, {});
      const rowValues = seenByRoll.get(rollNumber);
      Object.entries(dataset.dynamicFields || {}).forEach(([key, value]) => {
        keys.add(key);
        if (rowValues[key] !== undefined && String(rowValues[key]) !== String(value ?? '')) {
          keys.add(`${dataset.datasetType}_${key}`);
        }
        if (rowValues[key] === undefined || rowValues[key] === '') rowValues[key] = value;
      });
    });
    return [...keys];
  }, [columns, gridDatasets]);

  const dynamicColumnKeys = useMemo(() => {
    const keys = new Set(['RollNumber', 'FullName', 'Email', 'Major']);
    columns.forEach((col) => keys.add(col.key));
    spreadsheetColumns.forEach((key) => keys.add(key));
    return [...keys];
  }, [columns, spreadsheetColumns]);

  const manageableColumnKeys = useMemo(
    () => new Set(spreadsheetColumns.filter((column) => !systemColumns.includes(column))),
    [spreadsheetColumns]
  );

  const spreadsheetRows = useMemo(() => {
    const rowMap = new Map();
    gridDatasets.forEach((dataset) => {
      const dynamicFields = dataset.dynamicFields || {};
      const rollNumber = dataset.rollNumber || '';
      if (!rowMap.has(rollNumber)) {
        rowMap.set(rollNumber, {
          _id: rollNumber || dataset._id,
          RollNumber: rollNumber,
          FullName: dataset.studentId?.fullName || '',
          Email: dataset.studentId?.email || '',
          Major: dataset.studentId?.major || '',
          __fieldSources: {},
        });
      }
      const row = rowMap.get(rollNumber);
      row.FullName = row.FullName || dataset.studentId?.fullName || '';
      row.Email = row.Email || dataset.studentId?.email || '';
      row.Major = row.Major || dataset.studentId?.major || '';
      Object.entries(dynamicFields).forEach(([key, value]) => {
        if (row[key] === undefined || row[key] === '') {
          row[key] = value;
          row.__fieldSources[key] = { fieldKey: key, datasetType: dataset.datasetType };
        } else if (String(row[key]) !== String(value ?? '')) {
          const scopedKey = `${dataset.datasetType}_${key}`;
          row[scopedKey] = value;
          row.__fieldSources[scopedKey] = { fieldKey: key, datasetType: dataset.datasetType };
        }
      });
    });
    return [...rowMap.values()];
  }, [gridDatasets]);

  useEffect(() => {
    setExportColumns((current) => {
      const available = spreadsheetColumns;
      const kept = current.filter((key) => available.includes(key));
      const added = available.filter((key) => !kept.includes(key));
      return [...kept, ...added];
    });
  }, [spreadsheetColumns]);

  useEffect(() => {
    setSelectedExportColumns((current) => {
      const available = exportColumns;
      const kept = current.filter((key) => available.includes(key));
      const added = available.filter((key) => !kept.includes(key));
      const next = [...kept, ...added];
      return next.includes('RollNumber') ? next : ['RollNumber', ...next];
    });
  }, [exportColumns]);

  const selectedGridExportColumns = useMemo(() => (
    exportColumns.filter((column) => selectedExportColumns.includes(column))
  ), [exportColumns, selectedExportColumns]);

  const filteredSpreadsheetRows = useMemo(() => {
    const activeFilters = Object.entries(columnFilters).filter(([, value]) => String(value || '').trim());
    const filtered = spreadsheetRows.filter((row) => activeFilters.every(([column, value]) => (
      String(row[column] ?? '').toLowerCase().includes(String(value).trim().toLowerCase())
    )));
    return [...filtered].sort((a, b) => {
      if (groupColumn) {
        const leftGroup = String(a[groupColumn] ?? '').toLowerCase();
        const rightGroup = String(b[groupColumn] ?? '').toLowerCase();
        if (leftGroup !== rightGroup) return leftGroup > rightGroup ? 1 : -1;
      }
      const key = sortConfig.key;
      if (!key) return 0;
      const left = String(a[key] ?? '').toLowerCase();
      const right = String(b[key] ?? '').toLowerCase();
      if (left === right) return 0;
      const direction = sortConfig.direction === 'desc' ? -1 : 1;
      return left > right ? direction : -direction;
    });
  }, [columnFilters, groupColumn, sortConfig, spreadsheetRows]);

  const latestRollbackBatchId = useMemo(() => {
    const latestCommitted = history.find((batch) => batch.status === 'COMMITTED');
    if (!latestCommitted || latestCommitted.fileName?.startsWith('MANUAL_')) return null;
    return latestCommitted._id;
  }, [history]);

  const inspectFile = async (selectedFile) => {
    setFile(selectedFile);
    setPreview(null);
    setMappings([]);
    setSheets([]);
    setSheetName('');
    if (!selectedFile) return;
    setBusy('inspect');
    try {
      const res = await dataBankApi.inspectWorkbook(selectedFile);
      const workbookSheets = unwrap(res).sheets || [];
      setSheets(workbookSheets);
      setSheetName(workbookSheets[0]?.name || '');
    } catch (err) {
      toast.error(err.message || t('dataBankPage.workbookInspectionFailed'));
    } finally {
      setBusy('');
    }
  };

  const runPreview = async () => {
    if (!file) return toast.error(t('dataBankPage.pleaseSelectAnExcelFile'));
    if (!selectedClassId) return toast.error(t('dataBankPage.pleaseSelectAClassFirst'));
    if (!scope.classCode.trim()) return toast.error(t('dataBankPage.classcodeIsRequired'));
    setBusy('preview');
    try {
      const res = await dataBankApi.previewImport({ file, sheetName, headerRow, scope: { ...scope, classId: selectedClassId }, mappings });
      const batch = unwrap(res).batch;
      setPreview(batch);
      setMappings(batch.columnMappings || []);
      toast.success(t('dataBankPage.previewCreatedNoDatabaseChangesWere'));
    } catch (err) {
      toast.error(err.message || t('dataBankPage.previewFailed'));
    } finally {
      setBusy('');
    }
  };

  const updateMapping = (index, patch) => {
    setMappings((current) => current.map((mapping, idx) => (
      idx === index ? { ...mapping, ...patch, approved: patch.action === 'IGNORE_COLUMN' ? true : mapping.approved } : mapping
    )));
  };

  const approveMapping = (index) => {
    setMappings((current) => current.map((mapping, idx) => (
      idx === index
        ? { ...mapping, action: mapping.action === 'UNMAPPED' ? 'ADD_NEW_COLUMN' : mapping.action, approved: true }
        : mapping
    )));
  };

  const approveAllMappings = () => {
    setMappings((current) => current.map((mapping) => (
      mapping.action === 'IGNORE_COLUMN'
        ? mapping
        : { ...mapping, action: mapping.action === 'UNMAPPED' ? 'ADD_NEW_COLUMN' : mapping.action, approved: true }
    )));
  };

  const commitImport = async () => {
    if (!preview?._id || !file) return;
    const unresolved = mappings.filter((m) => !m.approved && m.action !== 'IGNORE_COLUMN');
    if (unresolved.length) return toast.error(t('dataBankPage.approveOrIgnoreAllUnknownColumns'));
    const highConflicts = preview.analysis?.conflicts?.filter((c) => c.severity === 'HIGH') || [];
    if (highConflicts.length && !confirmHighConflicts) return toast.error(t('dataBankPage.highConflictsRequireConfirmation'));
    setBusy('commit');
    try {
      const res = await dataBankApi.commitImport({ batchId: preview._id, file, mappings, confirmHighConflicts });
      setPreview(unwrap(res).batch);
      toast.success(t('dataBankPage.importCommittedWithSnapshotAndField'));
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.commitFailed'));
    } finally {
      setBusy('');
    }
  };

  const refreshExportPreview = async () => {
    if (!selectedClassId) return toast.error(t('dataBankPage.pleaseSelectAClassFirst'));
    if (filteredSpreadsheetRows.length === 0) {
      setExportPreviewRows([]);
      return toast.error(t('dataBankPage.noRowsMatchTheCurrentFilters'));
    }
    setBusy('exportPreview');
    try {
      const res = await dataBankApi.exportPreview({
        filters: { classId: selectedClassId },
        selectedColumns: selectedGridExportColumns,
        selectedRollNumbers: filteredSpreadsheetRows.map((row) => row.RollNumber).filter(Boolean),
      });
      setExportPreviewRows(unwrap(res).rows || []);
    } catch (err) {
      toast.error(err.message || t('dataBankPage.exportPreviewFailed'));
    } finally {
      setBusy('');
    }
  };

  const downloadExport = async () => {
    if (!selectedClassId) return toast.error(t('dataBankPage.pleaseSelectAClassFirst'));
    if (filteredSpreadsheetRows.length === 0) {
      return toast.error(t('dataBankPage.noRowsMatchTheCurrentFilters'));
    }
    setBusy('download');
    try {
      const response = await dataBankApi.downloadExport({
        filters: { classId: selectedClassId },
        selectedColumns: selectedGridExportColumns,
        selectedRollNumbers: filteredSpreadsheetRows.map((row) => row.RollNumber).filter(Boolean),
        headerAliases: {},
      });
      const blob = new Blob([response.data || response], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${scope.classCode || 'data-bank'}-all.xlsx`;
      link.click();
      window.URL.revokeObjectURL(url);
      toast.success(t('dataBankPage.exportDownloaded'));
    } catch (err) {
      toast.error(err.message || t('dataBankPage.exportFailed'));
    } finally {
      setBusy('');
    }
  };

  const rollback = async (batch) => {
    const confirmed = batch.status === 'COMMITTED' && await requestConfirm({
      title: t('dataBankPage.confirmRollback'),
      description: t('dataBankPage.rollbackOnlyLatestBatchByDefault'),
      confirmText: t('dataBankPage.rollback'),
    });
    if (!confirmed) return;
    setBusy(`rollback-${batch._id}`);
    try {
      await dataBankApi.rollback(batch._id, { forceSnapshotRestore: false });
      toast.success(t('dataBankPage.rollbackCompleted'));
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.rollbackFailed'));
    } finally {
      setBusy('');
    }
  };

  const saveTemplate = async () => {
    const name = await requestText({
      title: t('dataBankPage.saveExportTemplate'),
      label: t('dataBankPage.templateName'),
      placeholder: t('dataBankPage.trainingOfficeFormat'),
      required: true,
    });
    if (!name) return;
    try {
      await dataBankApi.saveTemplate({
        name,
        selectedColumns: selectedGridExportColumns,
        columnOrder: exportColumns,
        filters: { classId: selectedClassId },
        headerAliases: {},
      });
      toast.success(t('dataBankPage.templateSaved'));
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.templateSaveFailed'));
    }
  };

  const syncSelectedClass = async () => {
    if (!selectedClassId) return toast.error(t('dataBankPage.pleaseSelectAClassFirst'));
    setBusy('sync');
    try {
      const res = await dataBankApi.syncClass(selectedClassId);
      const result = unwrap(res);
      toast.success(t('dataBankPage.syncedStudentsAndTeams', { v1: result.studentCount || 0, v2: result.teamCount || 0 }));
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.classSyncFailed'));
    } finally {
      setBusy('');
    }
  };

  const addGridColumn = async () => {
    if (!selectedClassId) return toast.error(t('dataBankPage.pleaseSelectAClassFirst'));
    const fieldKey = await requestText({
      title: t('dataBankPage.addColumn'),
      label: t('dataBankPage.newColumnName'),
      placeholder: t('dataBankPage.checkpoint1'),
      required: true,
    });
    if (!fieldKey) return;
    try {
      await dataBankApi.addGridColumn({ classId: selectedClassId, fieldKey, displayName: fieldKey });
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.columnAddFailed'));
    }
  };

  const deleteGridColumn = async (fieldKey) => {
    if (!selectedClassId) return toast.error(t('dataBankPage.pleaseSelectAClassFirst'));
    if (systemColumns.includes(fieldKey)) return toast.error(t('dataBankPage.systemColumnsCannotBeDeleted'));
    const confirmed = await requestConfirm({
      title: t('dataBankPage.permanentlyDeleteColumn'),
      description: t('dataBankPage.permanentlyDeleteColumnAndAllOf', { fieldKey }),
      confirmText: t('dataBankPage.deletePermanently'),
    });
    if (!confirmed) return;
    try {
      await dataBankApi.deleteGridColumn({ classId: selectedClassId, fieldKey });
      setColumns((current) => current.filter((column) => column.key !== fieldKey));
      setGridDatasets((current) => current.map((dataset) => {
        if (!Object.prototype.hasOwnProperty.call(dataset.dynamicFields || {}, fieldKey)) return dataset;
        const dynamicFields = { ...dataset.dynamicFields };
        delete dynamicFields[fieldKey];
        return { ...dataset, dynamicFields };
      }));
      setColumnFilters((current) => {
        const next = { ...current };
        delete next[fieldKey];
        return next;
      });
      if (groupColumn === fieldKey) {
        setGroupColumn('');
        setGroupColumnInput('');
        localStorage.removeItem(groupStorageKey);
      }
      toast.success(t('dataBankPage.columnDeleted'));
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.columnDeleteFailed'));
    }
  };

  const addGridRow = async () => {
    if (!selectedClassId) return toast.error(t('dataBankPage.pleaseSelectAClassFirst'));
    const rollNumber = await requestText({
      title: t('dataBankPage.addRow'),
      label: t('dataBankPage.rollnumberForNewRow'),
      placeholder: 'DE180185',
      required: true,
    });
    if (!rollNumber) return;
    const fullName = await requestText({
      title: t('dataBankPage.addRow'),
      label: t('dataBankPage.fullnameOptional'),
      placeholder: t('dataBankPage.nguyenVanA'),
    }) || '';
    try {
      await dataBankApi.addGridRow({
        classId: selectedClassId,
        datasetType: scope.datasetType || 'CUSTOM',
        values: { RollNumber: rollNumber, FullName: fullName },
      });
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.rowAddFailed'));
    }
  };

  const deleteGridRow = async (rollNumber) => {
    if (!selectedClassId || !rollNumber) return;
    const confirmed = await requestConfirm({
      title: t('dataBankPage.deleteRow'),
      description: t('dataBankPage.deleteAllDataBankDatasetsFor', { rollNumber }),
      confirmText: t('dataBankPage.deleteRow2'),
    });
    if (!confirmed) return;
    try {
      await dataBankApi.deleteGridRow({ classId: selectedClassId, rollNumber });
      toast.success(t('dataBankPage.rowDeleted'));
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.rowDeleteFailed'));
    }
  };

  const startEditCell = (row, column) => {
    if (column === 'RollNumber') return;
    const source = row.__fieldSources?.[column];
    setSelectedCell({ rollNumber: row.RollNumber, column });
    setEditingCell({
      rollNumber: row.RollNumber,
      column,
      fieldKey: source?.fieldKey || column,
      datasetType: source?.datasetType || scope.datasetType || 'CUSTOM',
    });
    setEditingValue(String(row[column] ?? ''));
  };

  const saveEditingCell = async ({ moveDown = false } = {}) => {
    if (!editingCell || savingCellRef.current) return;
    savingCellRef.current = true;
    const savedCell = editingCell;
    try {
      if (editingValue.trim() === '') {
        const confirmed = await requestConfirm({
          title: t('dataBankPage.clearCell'),
          description: t('dataBankPage.clearValueFor', { column: editingCell.column }),
          confirmText: t('dataBankPage.clearCell2'),
        });
        if (!confirmed) return;
        await dataBankApi.clearGridCell({
          classId: selectedClassId,
          rollNumber: editingCell.rollNumber,
          fieldKey: editingCell.fieldKey,
          datasetType: editingCell.datasetType,
        });
      } else {
        await dataBankApi.editGridCell({
          classId: selectedClassId,
          rollNumber: editingCell.rollNumber,
          fieldKey: editingCell.fieldKey,
          value: editingValue,
          datasetType: editingCell.datasetType,
        });
      }
      setEditingCell(null);
      setEditingValue('');
      await loadReferenceData();
      if (moveDown) {
        const currentIndex = filteredSpreadsheetRows.findIndex((row) => row.RollNumber === savedCell.rollNumber);
        const nextRow = filteredSpreadsheetRows[currentIndex + 1];
        if (nextRow) startEditCell(nextRow, savedCell.column);
      }
    } catch (err) {
      toast.error(err.message || t('dataBankPage.cellUpdateFailed'));
    } finally {
      savingCellRef.current = false;
    }
  };

  const finishFillDrag = async () => {
    const drag = fillDragRef.current;
    fillDragRef.current = null;
    setFillRange(null);
    if (!drag || drag.targetIndex === drag.sourceIndex) return;

    const start = Math.min(drag.sourceIndex, drag.targetIndex);
    const end = Math.max(drag.sourceIndex, drag.targetIndex);
    const targetRows = filteredSpreadsheetRows
      .slice(start, end + 1)
      .filter((_, offset) => start + offset !== drag.sourceIndex);
    if (!targetRows.length) return;

    const updates = targetRows.map((row) => {
      const source = row.__fieldSources?.[drag.column];
      return {
        rollNumber: row.RollNumber,
        fieldKey: source?.fieldKey || drag.fieldKey,
        datasetType: source?.datasetType || drag.datasetType,
        value: drag.value,
      };
    });
    try {
      await dataBankApi.editGridCellsBulk({ classId: selectedClassId, updates });
      setGridDatasets((current) => current.map((dataset) => {
        const update = updates.find((item) => (
          item.rollNumber === dataset.rollNumber
          && item.datasetType === dataset.datasetType
        ));
        if (!update || systemColumns.includes(update.fieldKey)) return dataset;
        return {
          ...dataset,
          dynamicFields: { ...dataset.dynamicFields, [update.fieldKey]: update.value },
        };
      }));
      await loadReferenceData();
    } catch (err) {
      toast.error(err.message || t('dataBankPage.fillCellsFailed'));
    }
  };

  useEffect(() => {
    const handleMouseUp = () => finishFillDrag();
    window.addEventListener('mouseup', handleMouseUp);
    return () => window.removeEventListener('mouseup', handleMouseUp);
  }, [filteredSpreadsheetRows, selectedClassId]);

  const updateSort = (column) => {
    setSortConfig((current) => ({
      key: column,
      direction: current.key === column && current.direction === 'asc' ? 'desc' : 'asc',
    }));
  };

  const applyGroupColumn = () => {
    const requested = groupColumnInput.trim().toLowerCase();
    if (!requested) {
      setGroupColumn('');
      localStorage.removeItem(groupStorageKey);
      toast.success(t('dataBankPage.groupingCleared'));
      return;
    }
    const matchedColumn = spreadsheetColumns.find((column) => column.toLowerCase() === requested)
      || spreadsheetColumns.find((column) => column.toLowerCase().includes(requested));
    if (!matchedColumn) return toast.error(t('dataBankPage.columnNotFound', { groupColumnInput }));
    setGroupColumn(matchedColumn);
    setGroupColumnInput(matchedColumn);
    if (rememberGrouping) localStorage.setItem(groupStorageKey, matchedColumn);
    else localStorage.removeItem(groupStorageKey);
    toast.success(t('dataBankPage.groupedBy', { matchedColumn }));
  };

  const clearGroupColumn = () => {
    setGroupColumn('');
    setGroupColumnInput('');
    localStorage.removeItem(groupStorageKey);
    toast.success(t('dataBankPage.groupingCleared'));
  };

  const applyTemplateColumns = (template) => {
    const available = new Set(spreadsheetColumns);
    const selected = (template.selectedColumns || []).filter((column) => available.has(column));
    const ordered = (template.columnOrder || []).filter((column) => available.has(column));
    const remaining = spreadsheetColumns.filter((column) => !ordered.includes(column));
    const orderedColumns = [...ordered, ...remaining];
    setExportColumns(orderedColumns.length ? orderedColumns : ['RollNumber']);
    setSelectedExportColumns(
      selected.includes('RollNumber') ? selected : ['RollNumber', ...selected]
    );
  };

  const applyTemplate = (template) => {
    const templateClassId = template.filters?.classId;
    if (templateClassId && templateClassId !== selectedClassId) {
      pendingTemplateRef.current = template;
      applyClassToScope(templateClassId);
      return;
    }
    applyTemplateColumns(template);
  };

  useEffect(() => {
    const pendingTemplate = pendingTemplateRef.current;
    if (!pendingTemplate || loadingData) return;
    if (pendingTemplate.filters?.classId !== selectedClassId) return;
    applyTemplateColumns(pendingTemplate);
    pendingTemplateRef.current = null;
  }, [loadingData, selectedClassId, spreadsheetColumns]);

  const handleExportColumnDragEnd = ({ active, over }) => {
    if (!over || active.id === over.id) return;
    setExportColumns((items) => arrayMove(items, items.indexOf(active.id), items.indexOf(over.id)));
  };

  const toggleExportColumn = (column, checked) => {
    if (column === 'RollNumber') return;
    setSelectedExportColumns((current) => {
      if (checked) return [...new Set([...current, column])];
      return current.filter((key) => key !== column);
    });
  };

  const selectAllExportColumns = () => {
    setSelectedExportColumns(exportColumns);
  };

  const restoreExportColumns = () => {
    setExportColumns(spreadsheetColumns);
    setSelectedExportColumns(spreadsheetColumns);
  };

  const issueCount = preview?.analysis?.blockingIssues?.length || 0;
  const conflictCount = preview?.analysis?.conflicts?.length || 0;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-heading sm:text-display text-secondary tracking-tight">{t('dataBankPage.classDataBank')}</h1>
          <p className="mt-1 text-sm text-slate-500">{t('dataBankPage.previewFirstExcelImportExportWith')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" icon={RefreshCw} isLoading={loadingData} onClick={() => loadReferenceData({ showLoading: true })}>{t('common:actions.refresh')}</Button>
          <Button icon={RefreshCw} isLoading={busy === 'sync'} disabled={loadingData || !selectedClassId} onClick={syncSelectedClass}>{t('dataBankPage.syncClassData')}</Button>
        </div>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-5">
        <label className="space-y-1">
          <span className="text-xs font-semibold text-slate-500">{t('dataBankPage.class')}</span>
          <select
            className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm"
            value={selectedClassId}
            onChange={(e) => applyClassToScope(e.target.value)}
          >
            <option value="">{t('dataBankPage.selectClass')}</option>
            {classes.map((cls) => (
              <option key={cls._id} value={cls._id}>
                {cls.classCode} - {cls.subjectCode} - {cls.semester}{String(cls.year || '').slice(-2)}
              </option>
            ))}
          </select>
        </label>
      </section>

      <section className="grid grid-cols-1 gap-3 md:grid-cols-4">
        {loadingData ? (
          <div className="col-span-full flex items-center justify-center rounded-lg border border-slate-200 bg-white p-6 text-sm text-slate-500">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" /> {t('dataBankPage.loadingDataBank')}
          </div>
        ) : ([
          ['Datasets', datasets.length],
          ['Import Batches', history.length],
          ['Columns', dynamicColumnKeys.length],
          ['Templates', templates.length],
        ].map(([label, value]) => (
          <div key={label} className="rounded-lg border border-slate-200 bg-white p-4">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">{label}</p>
            <p className="mt-2 text-2xl font-bold text-slate-900">{value}</p>
          </div>
        )))}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-5">
        <div className="flex items-center gap-2">
          <FileSpreadsheet className="h-5 w-5 text-primary" />
          <h2 className="text-lg font-semibold text-slate-900">{t('dataBankPage.importWorkflow')}</h2>
        </div>
        <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-4">
          <label className="space-y-1">
            <span className="text-xs font-semibold text-slate-500">{t('dataBankPage.semester')}</span>
            <input className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm" value={scope.semester} readOnly />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-semibold text-slate-500">{t('dataBankPage.subjectcode')}</span>
            <input className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm" value={scope.subjectCode} readOnly />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-semibold text-slate-500">{t('dataBankPage.classcode')}</span>
            <input className="w-full rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm" value={scope.classCode} readOnly placeholder={t('dataBankPage.exe1011')} />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-semibold text-slate-500">{t('dataBankPage.datasetType')}</span>
            <select
              className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm"
              value={scope.datasetType}
              onChange={(event) => {
                setScope((current) => ({ ...current, datasetType: event.target.value }));
                setPreview(null);
                setMappings([]);
              }}
            >
              {datasetTypes.map((type) => <option key={type} value={type}>{type}</option>)}
            </select>
          </label>
        </div>

        <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-4">
          <label className="lg:col-span-2 flex min-h-24 cursor-pointer flex-col items-center justify-center rounded-lg border border-dashed border-slate-300 bg-slate-50 px-4 py-5 text-center hover:bg-slate-100">
            <Upload className="mb-2 h-6 w-6 text-slate-400" />
            <span className="text-sm font-medium text-slate-700">{file?.name || t('dataBankPage.uploadXlsxXls')}</span>
            <input type="file" accept=".xlsx,.xls" className="hidden" onChange={(e) => inspectFile(e.target.files?.[0])} />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-semibold text-slate-500">{t('dataBankPage.sheet')}</span>
            <select className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm" value={sheetName} onChange={(e) => setSheetName(e.target.value)}>
              {sheets.map((sheet) => <option key={sheet.name} value={sheet.name}>{sheet.name}</option>)}
            </select>
          </label>
          <label className="space-y-1">
            <span className="text-xs font-semibold text-slate-500">{t('dataBankPage.headerRow')}</span>
            <input type="number" min="1" className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm" value={headerRow} onChange={(e) => setHeaderRow(e.target.value)} />
          </label>
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <Button icon={ShieldAlert} isLoading={busy === 'preview'} onClick={runPreview}>{t('dataBankPage.previewOnly')}</Button>
          <Button
            icon={CheckCircle2}
            isLoading={busy === 'commit'}
            disabled={!preview || preview.status === 'COMMITTED' || issueCount > 0}
            onClick={commitImport}
            className="border border-emerald-600 bg-emerald-600 text-white shadow-sm hover:bg-emerald-700 hover:shadow-md focus:ring-emerald-200"
          >
            {t('dataBankPage.commitImport')}
          </Button>
          {conflictCount > 0 && (
            <label className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-sm font-semibold shadow-sm transition-colors ${
              confirmHighConflicts ? 'border-emerald-300 bg-emerald-50 text-emerald-700' : 'border-amber-300 bg-amber-100 text-amber-900 hover:bg-amber-200'
            }`}>
              <input type="checkbox" checked={confirmHighConflicts} onChange={(e) => setConfirmHighConflicts(e.target.checked)} className="h-4 w-4 rounded border-amber-400 text-emerald-600 focus:ring-emerald-500" />
              {confirmHighConflicts ? t('dataBankPage.highConflictsConfirmed') : t('dataBankPage.confirmHighSeverityConflicts')}
            </label>
          )}
        </div>

        {preview && (
          <div className="mt-5 grid grid-cols-1 gap-4 xl:grid-cols-2">
            <div className="rounded-lg border border-slate-200">
              <div className="border-b border-slate-100 px-4 py-3">
                <h3 className="font-semibold text-slate-900">{t('dataBankPage.previewAnalysis')}</h3>
              </div>
              <div className="grid grid-cols-2 gap-3 p-4 text-sm">
                <div>{t('dataBankPage.rowsToInsert')} <b>{preview.analysis?.summary?.rowsToInsert || 0}</b></div>
                <div>{t('dataBankPage.rowsToUpdate')} <b>{preview.analysis?.summary?.rowsToUpdate || 0}</b></div>
                <div>{t('dataBankPage.missingRollnumbers')} <b>{preview.analysis?.summary?.missingRollNumbers || 0}</b></div>
                <div>{t('dataBankPage.conflicts')} <b>{conflictCount}</b></div>
              </div>
              {issueCount > 0 && (
                <div className="mx-4 mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                  <AlertTriangle className="mr-2 inline h-4 w-4" /> {t('dataBankPage.commitBlocked')} {preview.analysis.blockingIssues.join(', ')}
                </div>
              )}
              <div className="max-h-80 overflow-auto px-4 pb-4">
                {(preview.analysis?.conflicts || []).slice(0, 20).map((conflict, index) => (
                  <div key={`${conflict.row}-${conflict.fieldKey}-${index}`} className="mb-2 rounded-lg border border-slate-200 p-3 text-xs">
                    <span className="font-semibold">{conflict.severity}</span> {t('dataBankPage.row')} {conflict.row}, {conflict.rollNumber}, {conflict.fieldKey}: {String(conflict.oldValue ?? '')} {'->'} {String(conflict.newValue ?? '')}
                  </div>
                ))}
              </div>
            </div>

            <div className="rounded-lg border border-slate-200">
              <div className="flex items-center justify-between gap-3 border-b border-slate-100 px-4 py-3">
                <h3 className="font-semibold text-slate-900">{t('dataBankPage.columnMapping')}</h3>
                <button
                  type="button"
                  onClick={approveAllMappings}
                  disabled={mappings.every((mapping) => mapping.approved || mapping.action === 'IGNORE_COLUMN')}
                  className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-700 transition-colors hover:bg-emerald-100 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {t('dataBankPage.approveAll')}
                </button>
              </div>
              <div className="max-h-96 overflow-auto">
                <table className="w-full text-left text-sm">
                  <thead className="sticky top-0 bg-slate-50 text-xs uppercase text-slate-600">
                    <tr>
                      <th className="px-3 py-2">{t('dataBankPage.source')}</th>
                      <th className="px-3 py-2">{t('dataBankPage.action')}</th>
                      <th className="px-3 py-2">{t('dataBankPage.target')}</th>
                      <th className="px-3 py-2">{t('dataBankPage.approval')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {mappings.map((mapping, index) => (
                      <tr key={`${mapping.sourceHeader}-${index}`} className="border-t border-slate-100">
                        <td className="px-3 py-2 font-medium text-slate-800">{mapping.sourceHeader}</td>
                        <td className="px-3 py-2">
                          <select className="rounded border border-slate-200 px-2 py-1" value={mapping.action} onChange={(e) => updateMapping(index, { action: e.target.value })}>
                            {mappingActions.map((action) => <option key={action}>{action}</option>)}
                          </select>
                        </td>
                        <td className="px-3 py-2">
                          <input className="w-36 rounded border border-slate-200 px-2 py-1" value={mapping.targetKey || ''} onChange={(e) => updateMapping(index, { targetKey: e.target.value })} />
                        </td>
                        <td className="px-3 py-2">
                          {mapping.approved ? (
                            <span className="text-xs font-semibold text-green-600">{t('dataBankPage.approved')}</span>
                          ) : (
                            <button className="text-xs font-semibold text-primary-700" onClick={() => approveMapping(index)}>{t('dataBankPage.approve')}</button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </section>

      <section className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        <div className="rounded-lg border border-slate-200 bg-white p-5">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold text-slate-900">{t('dataBankPage.exportBuilder')}</h2>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" icon={Save} onClick={saveTemplate}>{t('dataBankPage.template')}</Button>
              <Button size="sm" icon={Download} isLoading={busy === 'download'} disabled={!selectedClassId || filteredSpreadsheetRows.length === 0} onClick={downloadExport}>{t('common:actions.download')}</Button>
            </div>
          </div>
          <div className="mt-4 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">
            {t('dataBankPage.tickColumnsAndDragColumnHeaders')} <b>{t('dataBankPage.classDataGrid')}</b>{t('dataBankPage.exportUsesTheSelectedColumnsCurrent')}
            <div className="mt-3 flex flex-wrap gap-2">
              <Button size="xs" variant="outline" onClick={selectAllExportColumns}>{t('dataBankPage.selectAllColumns')}</Button>
              <Button size="xs" variant="outline" onClick={restoreExportColumns}>{t('dataBankPage.resetColumnOrder')}</Button>
              <span className="rounded-lg bg-white px-3 py-1.5 text-xs font-semibold text-slate-500">
                {t('dataBankPage.columnsSelected', { length: selectedGridExportColumns.length, length2: exportColumns.length })}
              </span>
              <span className="rounded-lg bg-white px-3 py-1.5 text-xs font-semibold text-slate-500">
                {t('dataBankPage.rowsInCurrentView', { length: filteredSpreadsheetRows.length })}
              </span>
            </div>
          </div>
          <div className="mt-4 flex flex-wrap gap-2">
            {templates.map((template) => (
              <button key={template._id} className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600" onClick={() => applyTemplate(template)}>
                {template.name}
              </button>
            ))}
          </div>
          <Button className="mt-4" variant="outline" icon={RefreshCw} isLoading={busy === 'exportPreview'} disabled={!selectedClassId || filteredSpreadsheetRows.length === 0} onClick={refreshExportPreview}>{t('dataBankPage.previewExport')}</Button>
          <div className="mt-4 max-h-80 overflow-auto rounded-lg border border-slate-200">
            {exportPreviewRows.length === 0 ? <EmptyState title={t('dataBankPage.noExportPreview')} icon={Download} size="sm" /> : (
              <table className="w-full text-left text-xs">
                <thead className="sticky top-0 bg-slate-50">
                  <tr>{Object.keys(exportPreviewRows[0]).map((key) => <th key={key} className="px-3 py-2">{key}</th>)}</tr>
                </thead>
                <tbody>
                  {exportPreviewRows.map((row, index) => (
                    <tr key={index} className="border-t border-slate-100">
                      {Object.keys(exportPreviewRows[0]).map((key) => <td key={key} className="px-3 py-2">{String(row[key] ?? '')}</td>)}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        <div className="rounded-lg border border-slate-200 bg-white p-5">
          <div className="mb-4 flex items-center gap-2">
            <History className="h-5 w-5 text-primary" />
            <h2 className="text-lg font-semibold text-slate-900">{t('dataBankPage.importHistory')}</h2>
          </div>
          <div className="max-h-[34rem] overflow-auto">
            {history.map((batch) => (
              <div key={batch._id} className="mb-3 rounded-lg border border-slate-200 p-3">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold text-slate-900">{batch.fileName}</p>
                    <p className="text-xs text-slate-500">{batch.semester} / {batch.subjectCode} / {batch.classCode}</p>
                    <p className="mt-1 text-xs text-slate-500">
                      {t('dataBankPage.uploaded', { v1: formatDateTime(batch.createdAt), v2: batch.committedAt && ` | Committed: ${formatDateTime(batch.committedAt)}`, v3: batch.rolledBackAt && ` | Rolled back: ${formatDateTime(batch.rolledBackAt)}` })}
                    </p>
                    <p className="mt-1 text-xs text-slate-500">{t('dataBankPage.insertedUpdatedSkipped', { v1: batch.rowsInserted || 0, v2: batch.rowsUpdated || 0, v3: batch.rowsSkipped || 0 })}</p>
                  </div>
                  <span className="rounded-full bg-slate-100 px-2 py-1 text-xs font-semibold text-slate-600">{batch.status}</span>
                </div>
                {batch._id === latestRollbackBatchId && (
                  <Button className="mt-3" size="xs" variant="outline" icon={busy === `rollback-${batch._id}` ? Loader2 : ArchiveRestore} onClick={() => rollback(batch)}>{t('dataBankPage.rollback')}</Button>
                )}
              </div>
            ))}
            {history.length === 0 && <EmptyState title={t('dataBankPage.noImportsYet')} icon={History} size="sm" />}
          </div>
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white">
        <div className="flex flex-col gap-3 border-b border-slate-100 p-5 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">{t('dataBankPage.classDataGrid')}</h2>
            <p className="mt-1 text-sm text-slate-500">{t('dataBankPage.editCellsAddDeleteRowsOr')}</p>
            {groupColumn && <p className="mt-1 text-xs font-semibold text-primary-700">{t('dataBankPage.groupingRowsBy', { groupColumn })}</p>}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-2 py-1.5">
              <input
                className="h-8 w-40 rounded border border-slate-200 bg-white px-2 text-xs outline-none focus:border-primary"
                value={groupColumnInput}
                onChange={(e) => setGroupColumnInput(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') applyGroupColumn(); }}
                placeholder={t('dataBankPage.groupByColumn')}
              />
              <button type="button" onClick={applyGroupColumn} className="h-8 rounded bg-primary px-3 text-xs font-semibold text-white hover:bg-primary-dark">
                {t('dataBankPage.group')}
              </button>
              {groupColumn && (
                <button type="button" onClick={clearGroupColumn} className="h-8 rounded border border-slate-200 bg-white px-2 text-xs font-semibold text-slate-600 hover:bg-slate-100">
                  {t('common:actions.clear')}
                </button>
              )}
              <label className="flex items-center gap-1 text-xs font-medium text-slate-600">
                <input
                  type="checkbox"
                  checked={rememberGrouping}
                  onChange={(e) => {
                    const checked = e.target.checked;
                    setRememberGrouping(checked);
                    if (checked && groupColumn) localStorage.setItem(groupStorageKey, groupColumn);
                    if (!checked) localStorage.removeItem(groupStorageKey);
                  }}
                />
                {t('dataBankPage.remember')}
              </label>
            </div>
            <Button size="sm" variant="outline" icon={Filter} onClick={() => setColumnFilters({})}>{t('dataBankPage.clearFilters')}</Button>
            <Button size="sm" variant="outline" icon={Plus} onClick={addGridColumn}>{t('dataBankPage.addColumn2')}</Button>
            <Button size="sm" icon={Plus} onClick={addGridRow}>{t('dataBankPage.addRow2')}</Button>
          </div>
        </div>

        {loadingData ? (
          <div className="flex items-center justify-center p-10 text-sm text-slate-500">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" /> {t('dataBankPage.loadingClassData')}
          </div>
        ) : !selectedClassId ? (
          <EmptyState title={t('dataBankPage.selectAClassToViewData')} icon={FileSpreadsheet} size="sm" />
        ) : filteredSpreadsheetRows.length === 0 ? (
          <div className="p-6">
            <EmptyState title={t('dataBankPage.noRowsMatch')} description={t('dataBankPage.clearFiltersSyncClassDataOr')} icon={FileSpreadsheet} size="sm" />
          </div>
        ) : (
          <div className="overflow-auto">
            <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleExportColumnDragEnd}>
              <table className="min-w-full border-separate border-spacing-0 text-left text-xs">
                <thead>
                  <tr>
                    <th className="sticky left-0 top-0 z-20 w-16 border-b border-r border-slate-200 bg-slate-100 px-3 py-2 text-center font-semibold text-slate-600">#</th>
                    <SortableContext items={exportColumns}>
                      {exportColumns.map((column, index) => {
                        const isChecked = selectedExportColumns.includes(column);
                        return (
                          <SortableHeaderCell
                            key={column}
                            id={column}
                            className={`sticky top-0 z-10 min-w-44 border-b border-r border-slate-200 bg-slate-100 px-3 py-2 font-semibold text-slate-700 ${index === 0 ? 'left-16 z-20' : ''}`}
                          >
                            <div className="min-w-0 flex-1">
                              <div className="flex items-start justify-between gap-2">
                                <label className="flex min-w-0 items-start gap-2">
                                  <input
                                    type="checkbox"
                                    className="mt-0.5 h-3.5 w-3.5 rounded border-slate-300 text-primary-700 focus:ring-primary"
                                    checked={isChecked}
                                    disabled={column === 'RollNumber'}
                                    onChange={(e) => toggleExportColumn(column, e.target.checked)}
                                    title={t('dataBankPage.includeInExport')}
                                  />
                                  <button type="button" className="min-w-0 truncate text-left font-semibold hover:text-primary-700" onClick={() => updateSort(column)}>
                                    {column}{sortConfig.key === column ? (sortConfig.direction === 'asc' ? ' ASC' : ' DESC') : ''}
                                  </button>
                                </label>
                                {manageableColumnKeys.has(column) && (
                                  <button type="button" className="shrink-0 text-slate-400 hover:text-danger" onClick={() => deleteGridColumn(column)} title={t('dataBankPage.deleteColumn')}>
                                    <Trash2 className="h-3.5 w-3.5" />
                                  </button>
                                )}
                              </div>
                              <input
                                className="mt-2 w-full rounded border border-slate-200 bg-white px-2 py-1 text-xs font-normal"
                                value={columnFilters[column] || ''}
                                onChange={(e) => setColumnFilters((current) => ({ ...current, [column]: e.target.value }))}
                                placeholder={t('dataBankPage.filter')}
                              />
                            </div>
                          </SortableHeaderCell>
                        );
                      })}
                    </SortableContext>
                  </tr>
                </thead>
                <tbody>
                  {filteredSpreadsheetRows.map((row, rowIndex) => (
                    <tr key={row._id} className="hover:bg-primary-50/30">
                      <td className="sticky left-0 z-10 border-b border-r border-slate-200 bg-slate-50 px-2 py-2 text-center font-medium text-slate-400">
                        <div className="flex items-center justify-center gap-1">
                          <span>{rowIndex + 1}</span>
                          <button type="button" className="text-slate-300 hover:text-danger" onClick={() => deleteGridRow(row.RollNumber)} title={t('dataBankPage.deleteRow')}>
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </td>
                      {exportColumns.map((column, columnIndex) => {
                        const isSelected = selectedCell?.rollNumber === row.RollNumber && selectedCell?.column === column;
                        const isEditing = editingCell?.rollNumber === row.RollNumber && editingCell?.column === column;
                        const isInFillRange = fillRange?.column === column
                          && rowIndex >= Math.min(fillRange.sourceIndex, fillRange.targetIndex)
                          && rowIndex <= Math.max(fillRange.sourceIndex, fillRange.targetIndex);
                        return (
                          <td
                            key={`${row._id}-${column}`}
                            tabIndex={column === 'RollNumber' ? -1 : 0}
                            className={`relative max-w-72 border-b border-r px-3 py-2 text-slate-700 outline-none ${
                              columnIndex === 0 ? 'sticky left-16 z-10 bg-white font-semibold text-slate-900' : ''
                            } ${
                              isSelected ? 'border-primary bg-primary-50 ring-2 ring-inset ring-primary' : 'border-slate-100'
                            } ${
                              isInFillRange && !isSelected ? 'bg-primary-50/70 ring-1 ring-inset ring-primary-200' : ''
                            }`}
                            title={String(row[column] ?? '')}
                            onClick={() => {
                              if (column !== 'RollNumber') setSelectedCell({ rollNumber: row.RollNumber, column });
                            }}
                            onDoubleClick={() => startEditCell(row, column)}
                            onMouseEnter={() => {
                              const drag = fillDragRef.current;
                              if (!drag || drag.column !== column) return;
                              drag.targetIndex = rowIndex;
                              setFillRange({
                                column,
                                sourceIndex: drag.sourceIndex,
                                targetIndex: rowIndex,
                              });
                            }}
                            onKeyDown={(event) => {
                              if (event.key === 'Enter' && !isEditing && column !== 'RollNumber') {
                                event.preventDefault();
                                startEditCell(row, column);
                              }
                            }}
                          >
                            {isEditing ? (
                              <input
                                autoFocus
                                className="w-full rounded border border-primary-200 px-2 py-1 text-xs"
                                value={editingValue}
                                onChange={(e) => setEditingValue(e.target.value)}
                                onBlur={() => saveEditingCell()}
                                onKeyDown={(e) => {
                                  if (e.key === 'Enter') {
                                    e.preventDefault();
                                    saveEditingCell({ moveDown: true });
                                  }
                                  if (e.key === 'Escape') {
                                    e.preventDefault();
                                    setEditingCell(null);
                                    setEditingValue('');
                                  }
                                }}
                              />
                            ) : (
                              <span className="block truncate">{String(row[column] ?? '')}</span>
                            )}
                            {isSelected && !isEditing && column !== 'RollNumber' && String(row[column] ?? '') !== '' && (
                              <button
                                type="button"
                                className="absolute -bottom-1 -right-1 z-20 h-2.5 w-2.5 cursor-crosshair border border-white bg-primary shadow-sm"
                                title={t('dataBankPage.dragToFillCells')}
                                aria-label={t('dataBankPage.fillCells', { column })}
                                onMouseDown={(event) => {
                                  event.preventDefault();
                                  event.stopPropagation();
                                  const source = row.__fieldSources?.[column];
                                  fillDragRef.current = {
                                    sourceIndex: rowIndex,
                                    targetIndex: rowIndex,
                                    column,
                                    fieldKey: source?.fieldKey || column,
                                    datasetType: source?.datasetType || scope.datasetType || 'CUSTOM',
                                    value: row[column] ?? '',
                                  };
                                  setFillRange({ column, sourceIndex: rowIndex, targetIndex: rowIndex });
                                }}
                              />
                            )}
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </DndContext>
          </div>
        )}
      </section>

      <Modal
        isOpen={Boolean(textDialog)}
        onClose={() => closeTextDialog(null)}
        title={textDialog?.title || ''}
        onSubmit={() => {
          const value = String(textDialog?.value || '').trim();
          if (textDialog?.required && !value) return toast.error(t('dataBankPage.pleaseEnterAValue'));
          closeTextDialog(value);
        }}
        submitText={t('common:actions.confirm')}
      >
        <label className="space-y-1">
          <span className="text-sm font-semibold text-slate-600">{textDialog?.label}</span>
          <input
            autoFocus
            className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-primary"
            value={textDialog?.value || ''}
            placeholder={textDialog?.placeholder || ''}
            onChange={(e) => setTextDialog((current) => ({ ...current, value: e.target.value }))}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                const value = String(textDialog?.value || '').trim();
                if (textDialog?.required && !value) return toast.error(t('dataBankPage.pleaseEnterAValue'));
                closeTextDialog(value);
              }
            }}
          />
        </label>
      </Modal>

      <Modal isOpen={Boolean(confirmDialog)} onClose={() => closeConfirmDialog(false)} title={confirmDialog?.title || ''}>
        <div className="space-y-5">
          <p className="text-sm text-slate-600">{confirmDialog?.description}</p>
          <div className="flex justify-end gap-3">
            <Button variant="outline" onClick={() => closeConfirmDialog(false)}>{t('common:actions.cancel')}</Button>
            <Button
              className={confirmDialog?.variant === 'danger' ? 'bg-red-500 text-white hover:bg-red-600' : ''}
              onClick={() => closeConfirmDialog(true)}
            >
              {confirmDialog?.confirmText || t('common:actions.confirm')}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};

export default DataBankPage;
