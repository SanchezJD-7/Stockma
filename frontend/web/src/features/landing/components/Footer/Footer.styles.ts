import styled from '@emotion/styled'
import { MutedText } from '../../../../styles/components/texts.styles'

export const SupportText = styled(MutedText)`
  padding: 10px 0;
  border-bottom: 1px solid var(--color-border);
  font-size: var(--fs-14);
  text-align: center;

  a {
    color: var(--color-text-primary);
    font-weight: 600;
    text-decoration: none;
  }

  a:hover {
    text-decoration: underline;
  }
`
