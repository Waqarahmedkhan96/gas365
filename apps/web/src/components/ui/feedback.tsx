import { useState, type ReactNode } from 'react';
import { AlertDialog, Dialog, Toast } from 'radix-ui';
import { Button } from './button';

export function Modal({
  title,
  description,
  trigger,
  children,
  open,
  onOpenChange,
  drawer = false,
  onOpenAutoFocus,
}: {
  title: string;
  description: string;
  trigger?: ReactNode;
  children: ReactNode;
  open?: boolean;
  onOpenChange?: (value: boolean) => void;
  drawer?: boolean;
  onOpenAutoFocus?: (event: Event) => void;
}) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      {trigger && <Dialog.Trigger asChild>{trigger}</Dialog.Trigger>}
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content
          onOpenAutoFocus={onOpenAutoFocus}
          className={drawer ? 'dialog-panel drawer-panel' : 'dialog-panel'}
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-xl font-semibold text-primary">
              {title}
            </Dialog.Title>
            <Dialog.Close asChild>
              <Button variant="ghost" size="icon" aria-label="Close dialog">
                ×
              </Button>
            </Dialog.Close>
          </div>
          <Dialog.Description className="mb-6 text-sm text-muted-foreground">
            {description}
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
    <AlertDialog.Root>
      <AlertDialog.Trigger asChild>
        <Button
          variant={destructive ? 'destructive' : 'outline'}
          disabled={disabled}
        >
          {children}
        </Button>
      </AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="dialog-overlay" />
        <AlertDialog.Content className="dialog-panel">
          <AlertDialog.Title className="text-xl font-semibold text-primary">
            {title}
          </AlertDialog.Title>
          <AlertDialog.Description className="my-4 text-muted-foreground">
            {description}
          </AlertDialog.Description>
          <div className="flex flex-wrap justify-end gap-3">
            <AlertDialog.Cancel asChild>
              <Button variant="outline">Cancel</Button>
            </AlertDialog.Cancel>
            <AlertDialog.Action asChild>
              <Button
                variant={destructive ? 'destructive' : 'default'}
                onClick={onConfirm}
              >
                Confirm
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
        <Toast.Title className="font-semibold">Saved successfully</Toast.Title>
        <Toast.Description>{message}</Toast.Description>
        <Toast.Close asChild>
          <Button variant="ghost" aria-label="Dismiss notification">
            Dismiss
          </Button>
        </Toast.Close>
      </Toast.Root>
      <Toast.Viewport className="toast-viewport" label="Notifications" />
    </Toast.Provider>
  );
}
