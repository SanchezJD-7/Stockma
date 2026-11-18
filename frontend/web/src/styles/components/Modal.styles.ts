import styled from '@emotion/styled'

export const Backdrop = styled.div`
  position: fixed;
  inset: 0;
  z-index: 1200;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: var(--color-backdrop);
`

export const Dialog = styled.div`
  position: relative;
  width: min(440px, 100%);
  max-height: calc(100vh - 32px);
  overflow-y: auto;
  padding: 28px 24px 24px;
  border-radius: 16px;
  background: var(--color-bg-surface);
  box-shadow: var(--color-shadow-dialog);
`

export const CloseButton = styled.button`
  position: absolute;
  top: 12px;
  right: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  padding: 0;
  border: none;
  border-radius: var(--radius-circle);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--fs-22);
  line-height: 1;
  cursor: pointer;

  &:hover {
    background: var(--color-primary-bg);
  }
`

export const Title = styled.h2<{ $withClose?: boolean }>`
  margin: ${({ $withClose }) => ($withClose ? '0 36px 4px 0' : '0 0 8px')};
  font-size: var(--fs-modal-title);
  font-weight: 600;
  line-height: 1.3;
`
