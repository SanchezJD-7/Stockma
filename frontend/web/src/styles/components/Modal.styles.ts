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
  padding: 32px 28px 28px;
  border-radius: 16px;
  background: var(--color-bg-surface);
  box-shadow: var(--color-shadow-dialog);
`

export const CloseButton = styled.button`
  position: absolute;
  top: 16px;
  right: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  padding: 0;
  border: none;
  border-radius: var(--radius-circle);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--fs-22);
  line-height: 1;
  cursor: pointer;
  transition:
    background-color 0.15s ease,
    color 0.15s ease;

  &:hover {
    color: var(--color-text-primary);
  }
`

export const Title = styled.h2<{ $withClose?: boolean }>`
  margin: ${({ $withClose }) => ($withClose ? '0 36px 6px 0' : '0 0 6px')};
  font-size: var(--fs-modal-title);
  font-weight: 700;
  line-height: 1.25;
  letter-spacing: -0.015em;
  color: var(--color-text-primary);
`

export const Subtitle = styled.p`
  margin: 0;
  font-size: var(--fs-14);
  line-height: 1.5;
  color: var(--color-text-secondary);
`
