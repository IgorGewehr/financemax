import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import type { CriarProjetoRequest } from '@/lib/api/financeiro';

interface ModalNovoProjetoProps {
  open: boolean;
  salvando: boolean;
  onClose: () => void;
  onSalvar: (input: CriarProjetoRequest) => Promise<unknown>;
}

/** Form "Novo projeto" (`POST /financeiro/projetos`) — nome obrigatório, descrição opcional. */
export function ModalNovoProjeto({ open, salvando, onClose, onSalvar }: ModalNovoProjetoProps) {
  const [nome, setNome] = useState('');
  const [descricao, setDescricao] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setNome('');
    setDescricao('');
    setErro(null);
  }, [open]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!nome.trim() || salvando) return;
    setErro(null);
    try {
      await onSalvar({ nome: nome.trim(), descricao: descricao.trim() || null });
      onClose();
    } catch {
      setErro('Não foi possível criar o projeto. Tente novamente.');
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Novo projeto">
      <form onSubmit={onSubmit} className="flex flex-col gap-3.5">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="projeto-nome" className="text-xs font-semibold text-muted-foreground">
            Nome
          </label>
          <Input
            id="projeto-nome"
            autoFocus
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            placeholder="Ex.: Consultoria Fiscal"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="projeto-descricao" className="text-xs font-semibold text-muted-foreground">
            Descrição <span className="font-normal text-faint">(opcional)</span>
          </label>
          <Input
            id="projeto-descricao"
            value={descricao}
            onChange={(e) => setDescricao(e.target.value)}
            placeholder="Ex.: Linha de assessoria recorrente"
          />
        </div>

        {erro && <p className="text-sm font-medium text-crit">{erro}</p>}

        <div className="mt-1 flex justify-end gap-2.5">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" variant="primary" size="sm" disabled={!nome.trim() || salvando}>
            {salvando ? 'Criando…' : 'Criar projeto'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
