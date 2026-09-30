import { useState, type ReactNode } from 'react';
import { AlertDialog, Dialog, Toast } from 'radix-ui';
import { X } from 'lucide-react';
import { Button } from './button';
import { tr } from '../../i18n';

export function Modal({
  title,
  description,
  trigger,
  children,
  open,
  onOpenChange,
  drawer = false,
  onOpenAutoFocus,
  onCloseAutoFocus,
}: {
  title: string;
  description: string;
  trigger?: ReactNode;
  children: ReactNode;
  open?: boolean;
  onOpenChange?: (value: boolean) => void;
  drawer?: boolean;
  onOpenAutoFocus?: (event: Event) => void;
  onCloseAutoFocus?: (event: Event) => void;
}) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      {trigger && <Dialog.Trigger asChild>{trigger}</Dialog.Trigger>}
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content
          onOpenAutoFocus={onOpenAutoFocus}
          onCloseAutoFocus={onCloseAutoFocus}
          className={drawer ? 'dialog-panel drawer-panel' : 'dialog-panel'}
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-xl font-semibold text-primary">
              {tr(title)}
            </Dialog.Title>
            <Dialog.Close asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label={tr('Close dialog')}
              >
                <X aria-hidden="true" size={18} />
              </Button>
            </Dialog.Close>
          </div>
          <Dialog.Description className="mb-6 text-sm text-muted-foreground">
            {tr(description)}
          </Dialog.Description>
          {children}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

export function ConfirmButton({
  title,
  description,
  children,
  onConfirm,
  disabled,
  destructive = false,
}: {
  title: string;
  description: string;
  children: ReactNode;
  onConfirm: () => void;
  disabled?: boolean;
  destructive?: boolean;
}) {
  return (
    <ConfirmDialog
      title={title}
      description={description}
      onConfirm={onConfirm}
      destructive={destructive}
      trigger={
        <Button
          variant={destructive ? 'destructive' : 'outline'}
          disabled={disabled}
        >
          {children}
        </Button>
      }
    />
  );
}

// Controlled variant for confirmations started from menus, where the trigger unmounts when the menu closes.
export function ConfirmDialog({
  title,
  description,
  onConfirm,
  destructive = false,
  trigger,
  open,
  onOpenChange,
}: {
  title: string;
  description: string;
  onConfirm: () => void;
  destructive?: boolean;
  trigger?: ReactNode;
  open?: boolean;
  onOpenChange?: (value: boolean) => void;
}) {
  return (
    <AlertDialog.Root open={open} onOpenChange={onOpenChange}>
      {trigger && <AlertDialog.Trigger asChild>{trigger}</AlertDialog.Trigger>}
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="dialog-overlay" />
        <AlertDialog.Content className="dialog-panel">
          <AlertDialog.Title className="text-xl font-semibold text-primary">
            {tr(title)}
          </AlertDialog.Title>
          <AlertDialog.Description className="my-4 text-muted-foreground">
            {tr(description)}
          </AlertDialog.Description>
          <div className="flex flex-wrap justify-end gap-3">
            <AlertDialog.Cancel asChild>
              <Button variant="outline">{tr('Cancel')}</Button>
            </AlertDialog.Cancel>
            <AlertDialog.Action asChild>
              <Button
                variant={destructive ? 'destructive' : 'default'}
                onClick={onConfirm}
              >
                {tr('Confirm')}
              </Button>
            </AlertDialog.Action>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}

export function Notification({ message }: { message: string }) {
  const [open, setOpen] = useState(true);
  return (
    <Toast.Provider swipeDirection="right" duration={7000}>
      <Toast.Root
        type="background"
        className="toast"
        open={open}
        onOpenChange={setOpen}
      >
        <Toast.Title className="font-semibold">
          {tr('Saved successfully')}
        </Toast.Title>
        <Toast.Description>{message}</Toast.Description>
        <Toast.Close asChild>
          <Button variant="ghost" aria-label={tr('Dismiss notification')}>
            {tr('Dismiss')}
          </Button>
        </Toast.Close>
      </Toast.Root>
      <Toast.Viewport className="toast-viewport" label={tr('Notifications')} />
    </Toast.Provider>
  );
}
